using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Coordinates queue lifecycle, engine execution, retries, history, and scheduler actions.</summary>
public sealed class DownloadCoordinatorService : IDownloadCoordinatorService
{
    private readonly IDownloadInputParserService _inputParser;
    private readonly IDownloadEngineResolver _engineResolver;
    private readonly IDownloadCategoryService _categoryService;
    private readonly IDownloaderSettingsService _settingsService;
    private readonly IDownloadHistoryService _historyService;
    private readonly IDownloadEventLogService _eventLog;
    private readonly IDownloadSchedulerService _scheduler;

    private readonly ConcurrentDictionary<Guid, ActiveJobHandle> _activeJobs = new();
    private readonly object _sync = new();

    private DownloaderSettings _settings;
    private int _queueOrderSeed;
    private Task? _queueLoopTask;
    private CancellationTokenSource? _queueCts;

    public ObservableCollection<DownloadJob> Jobs { get; } = [];

    public ObservableCollection<DownloadPackage> Packages { get; } = [];

    public ObservableCollection<DownloadHistoryEntry> History { get; } = [];

    public DownloadStatisticsSnapshot Statistics { get; } = new();

    public bool IsQueueRunning { get; private set; }

    public DownloadCoordinatorService(
        IDownloadInputParserService inputParser,
        IDownloadEngineResolver engineResolver,
        IDownloadCategoryService categoryService,
        IDownloaderSettingsService settingsService,
        IDownloadHistoryService historyService,
        IDownloadEventLogService eventLog,
        IDownloadSchedulerService scheduler)
    {
        _inputParser = inputParser;
        _engineResolver = engineResolver;
        _categoryService = categoryService;
        _settingsService = settingsService;
        _historyService = historyService;
        _eventLog = eventLog;
        _scheduler = scheduler;
        _settings = settingsService.Load();

        _scheduler.ActionTriggered += OnScheduledActionTriggered;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = _settingsService.Load();

        var history = await _historyService.LoadAsync(cancellationToken);
        await RunOnUiAsync(() =>
        {
            History.Clear();
            foreach (var entry in history)
            {
                History.Add(entry);
            }
        });

        RecomputeStatistics();
    }

    public async Task<int> AddFromInputAsync(string input, DownloaderMode mode, bool startImmediately, CancellationToken cancellationToken = default)
    {
        var urls = _inputParser.ExtractCandidateUrls(input);
        if (urls.Count == 0)
        {
            return 0;
        }

        var existing = await RunOnUiAsync(() => Jobs.Select(job => job.SourceUrl).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var inserted = 0;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_settings.General.DuplicateHandlingMode == DuplicateHandlingMode.Skip && existing.Contains(url))
            {
                continue;
            }

            existing.Add(url);

            var extension = GetExtension(url);
            var category = _categoryService.ResolveCategory(url, extension, _settings);

            var uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : null;
            var packageTitle = uri?.Host ?? "Batch";
            var packageId = packageTitle.ToLowerInvariant();

            var job = new DownloadJob
            {
                SourceUrl = url,
                Mode = mode,
                QueueOrder = Interlocked.Increment(ref _queueOrderSeed),
                PackageId = packageId,
                PackageTitle = packageTitle,
                Category = category.Name,
                Priority = category.PriorityOverride != DownloadPriority.Normal
                    ? category.PriorityOverride
                    : _settings.Queue.DefaultPriority,
                OutputDirectory = BuildOutputDirectory(category.Name, uri?.Host),
                RequestedFileName = InferFileName(url),
                Status = _settings.General.StageLinksBeforeDownload && !startImmediately
                    ? DownloadJobStatus.Staged
                    : DownloadJobStatus.Queued,
                StatusMessage = _settings.General.StageLinksBeforeDownload && !startImmediately
                    ? "Staged"
                    : "Queued",
            };

            var resolution = await _engineResolver.ResolveAsync(job, _settings, cancellationToken);
            ApplyProbe(job, resolution.Probe);
            job.EngineType = resolution.Engine.EngineType;

            await RunOnUiAsync(() =>
            {
                Jobs.Add(job);
                AttachToPackage(job);
            });

            inserted++;
            _eventLog.Log(DownloaderLogLevel.Normal, $"Job added ({job.EngineType}): {job.SourceUrl}", job.JobId);
        }

        RecomputeStatistics();

        var shouldStart = startImmediately || _settings.General.AutoStartOnAdd;
        if (shouldStart)
        {
            await StartQueueAsync(cancellationToken);
        }

        return inserted;
    }

    public async Task<int> AddAssetsAsync(
        IEnumerable<DownloadAssetCandidate> assets,
        DownloaderMode mode,
        bool startImmediately,
        CancellationToken cancellationToken = default)
    {
        var selected = assets
            .Where(asset => asset.IsSelected)
            .ToList();

        if (selected.Count == 0)
        {
            return 0;
        }

        var unique = selected
            .GroupBy(asset => asset.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var added = 0;
        foreach (var asset in unique)
        {
            var input = asset.Url;
            if (await AddFromInputAsync(input, mode, startImmediately, cancellationToken) > 0)
            {
                added++;
            }
        }

        return added;
    }

    public Task StartQueueAsync(CancellationToken cancellationToken = default)
    {
        if (IsQueueRunning)
        {
            return Task.CompletedTask;
        }

        IsQueueRunning = true;
        _queueCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _queueLoopTask = Task.Run(() => QueueLoopAsync(_queueCts.Token), _queueCts.Token);
        _eventLog.Log(DownloaderLogLevel.Normal, "Queue started.");
        return Task.CompletedTask;
    }

    public async Task PauseQueueAsync(CancellationToken cancellationToken = default)
    {
        if (!IsQueueRunning)
        {
            return;
        }

        IsQueueRunning = false;
        foreach (var handle in _activeJobs.Values)
        {
            handle.Reason = JobStopReason.Pause;
            handle.Cancellation.Cancel();
        }

        if (_queueLoopTask is not null)
        {
            await _queueLoopTask;
        }

        _eventLog.Log(DownloaderLogLevel.Normal, "Queue paused.");
    }

    public async Task StopQueueAsync(CancellationToken cancellationToken = default)
    {
        if (!IsQueueRunning && _activeJobs.IsEmpty)
        {
            return;
        }

        IsQueueRunning = false;

        if (_queueCts is not null)
        {
            _queueCts.Cancel();
        }

        foreach (var handle in _activeJobs.Values)
        {
            handle.Reason = JobStopReason.Cancel;
            handle.Cancellation.Cancel();
        }

        if (_queueLoopTask is not null)
        {
            await _queueLoopTask;
        }

        _eventLog.Log(DownloaderLogLevel.Normal, "Queue stopped.");
    }

    public void PauseJobs(IEnumerable<DownloadJob> jobs)
    {
        foreach (var job in jobs)
        {
            if (_activeJobs.TryGetValue(job.JobId, out var handle))
            {
                handle.Reason = JobStopReason.Pause;
                handle.Cancellation.Cancel();
            }
            else if (job.Status is DownloadJobStatus.Queued or DownloadJobStatus.Staged)
            {
                job.Status = DownloadJobStatus.Paused;
                job.StatusMessage = "Paused";
            }
        }

        RecomputeStatistics();
    }

    public void ResumeJobs(IEnumerable<DownloadJob> jobs)
    {
        foreach (var job in jobs)
        {
            if (job.Status == DownloadJobStatus.Paused)
            {
                job.Status = DownloadJobStatus.Queued;
                job.StatusMessage = "Queued";
            }
        }

        RecomputeStatistics();
    }

    public void CancelJobs(IEnumerable<DownloadJob> jobs)
    {
        foreach (var job in jobs)
        {
            if (_activeJobs.TryGetValue(job.JobId, out var handle))
            {
                handle.Reason = JobStopReason.Cancel;
                handle.Cancellation.Cancel();
            }
            else
            {
                job.Status = DownloadJobStatus.Cancelled;
                job.StatusMessage = "Cancelled";
            }
        }

        RecomputeStatistics();
    }

    public void RetryJobs(IEnumerable<DownloadJob> jobs)
    {
        foreach (var job in jobs)
        {
            if (job.Status is DownloadJobStatus.Failed or DownloadJobStatus.Cancelled or DownloadJobStatus.Paused)
            {
                ResetJobForRetry(job);
            }
        }

        RecomputeStatistics();
    }

    public void RemoveJobs(IEnumerable<DownloadJob> jobs)
    {
        var toRemove = jobs.ToList();
        foreach (var job in toRemove)
        {
            if (_activeJobs.TryGetValue(job.JobId, out var handle))
            {
                handle.Reason = JobStopReason.Cancel;
                handle.Cancellation.Cancel();
            }

            Jobs.Remove(job);
        }

        RebuildPackages();
        RecomputeStatistics();
    }

    public void ClearCompleted()
    {
        var removable = Jobs.Where(job => job.Status == DownloadJobStatus.Completed || job.Status == DownloadJobStatus.Skipped).ToList();
        RemoveJobs(removable);
    }

    public void ClearFailed()
    {
        var removable = Jobs.Where(job => job.Status is DownloadJobStatus.Failed or DownloadJobStatus.Cancelled).ToList();
        RemoveJobs(removable);
    }

    public void MoveJobsToTop(IEnumerable<DownloadJob> jobs)
    {
        var selected = jobs.OrderBy(job => job.QueueOrder).ToList();
        var baseOrder = Jobs.Min(job => job.QueueOrder) - selected.Count - 1;

        for (var i = 0; i < selected.Count; i++)
        {
            selected[i].QueueOrder = baseOrder + i;
        }
    }

    public void MoveJobsToBottom(IEnumerable<DownloadJob> jobs)
    {
        var selected = jobs.OrderBy(job => job.QueueOrder).ToList();
        var baseOrder = Jobs.Max(job => job.QueueOrder) + 1;

        for (var i = 0; i < selected.Count; i++)
        {
            selected[i].QueueOrder = baseOrder + i;
        }
    }

    public void RecomputeStatistics()
    {
        Statistics.Queued = Jobs.Count(job => job.Status == DownloadJobStatus.Queued || job.Status == DownloadJobStatus.Staged);
        Statistics.Active = Jobs.Count(job => job.Status is DownloadJobStatus.Downloading or DownloadJobStatus.Probing or DownloadJobStatus.Processing);
        Statistics.Paused = Jobs.Count(job => job.Status == DownloadJobStatus.Paused);
        Statistics.Completed = Jobs.Count(job => job.Status == DownloadJobStatus.Completed);
        Statistics.Failed = Jobs.Count(job => job.Status == DownloadJobStatus.Failed);
        Statistics.Skipped = Jobs.Count(job => job.Status == DownloadJobStatus.Skipped);
        Statistics.Cancelled = Jobs.Count(job => job.Status == DownloadJobStatus.Cancelled);

        foreach (var package in Packages)
        {
            var members = Jobs.Where(job => job.PackageId == package.PackageId).ToList();
            package.AssetCount = members.Count;
            package.CompletedCount = members.Count(job => job.Status == DownloadJobStatus.Completed);
            package.ProgressPercent = members.Count == 0
                ? 0
                : members.Average(job => job.ProgressPercent);
        }
    }

    public void ReloadSettings()
    {
        _settings = _settingsService.Load();
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _historyService.ClearAsync(cancellationToken);
        await RunOnUiAsync(() => History.Clear());
    }

    private async Task QueueLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var maxConcurrent = Math.Clamp(_settings.Queue.MaxConcurrentDownloads, 1, 16);

                while (_activeJobs.Count < maxConcurrent)
                {
                    var nextJob = await RunOnUiAsync(() => Jobs
                        .Where(job => job.Status == DownloadJobStatus.Queued)
                        .OrderByDescending(job => job.Priority)
                        .ThenBy(job => job.QueueOrder)
                        .FirstOrDefault());

                    if (nextJob is null)
                    {
                        break;
                    }

                    var handle = new ActiveJobHandle(new CancellationTokenSource());
                    if (!_activeJobs.TryAdd(nextJob.JobId, handle))
                    {
                        break;
                    }

                    _ = Task.Run(() => ExecuteJobAsync(nextJob, handle, cancellationToken), cancellationToken);
                }

                var hasQueued = await RunOnUiAsync(() => Jobs.Any(job => job.Status == DownloadJobStatus.Queued));
                if (!hasQueued && _activeJobs.IsEmpty)
                {
                    IsQueueRunning = false;
                    break;
                }

                await Task.Delay(200, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop.
        }
        finally
        {
            IsQueueRunning = false;
            _queueCts?.Dispose();
            _queueCts = null;
        }
    }

    private async Task ExecuteJobAsync(DownloadJob job, ActiveJobHandle handle, CancellationToken queueCancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(queueCancellationToken, handle.Cancellation.Token);

        var started = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            job.Status = DownloadJobStatus.Probing;
            job.StatusMessage = "Probing";
            job.StartedAt = started;

            var resolution = await _engineResolver.ResolveAsync(job, _settings, linked.Token);
            ApplyProbe(job, resolution.Probe);
            job.EngineType = resolution.Engine.EngineType;

            var targetFileName = string.IsNullOrWhiteSpace(job.RequestedFileName)
                ? resolution.Probe.SuggestedFileName
                : job.RequestedFileName;

            var plan = new DownloadExecutionPlan
            {
                TargetDirectory = string.IsNullOrWhiteSpace(job.OutputDirectory)
                    ? _settings.General.DefaultDownloadFolder
                    : job.OutputDirectory,
                TargetFileName = string.IsNullOrWhiteSpace(targetFileName)
                    ? "download.bin"
                    : targetFileName,
                OverwriteExisting = _settings.General.DuplicateHandlingMode == DuplicateHandlingMode.Overwrite,
                AutoRenameExisting = _settings.General.DuplicateHandlingMode == DuplicateHandlingMode.AutoRename,
                MediaProfile = BuildMediaProfile(job),
            };

            var progress = new Progress<DownloadProgressUpdate>(update => ApplyProgress(job, update));
            await resolution.Engine.ExecuteAsync(job, _settings, plan, progress, linked.Token);

            job.Status = DownloadJobStatus.Completed;
            job.ProgressPercent = 100;
            job.StatusMessage = "Completed";
            job.CompletedAt = DateTimeOffset.Now;
            job.NotifyDerivedMetrics();

            await AppendHistoryAsync(job);
            _eventLog.Log(DownloaderLogLevel.Normal, $"Completed: {job.DisplayTitle}", job.JobId);
        }
        catch (OperationCanceledException)
        {
            var reason = handle.Reason;
            job.Status = reason == JobStopReason.Pause ? DownloadJobStatus.Paused : DownloadJobStatus.Cancelled;
            job.StatusMessage = reason == JobStopReason.Pause ? "Paused" : "Cancelled";
            job.CompletedAt = DateTimeOffset.Now;

            if (reason != JobStopReason.Pause)
            {
                await AppendHistoryAsync(job);
            }

            _eventLog.Log(DownloaderLogLevel.Normal, $"{job.Status}: {job.DisplayTitle}", job.JobId);
        }
        catch (Exception ex)
        {
            if (job.RetryCount < Math.Max(0, _settings.Queue.MaxRetries))
            {
                job.RetryCount++;
                job.Status = DownloadJobStatus.Queued;
                job.StatusMessage = $"Retry {job.RetryCount}/{_settings.Queue.MaxRetries}";
                _eventLog.Log(DownloaderLogLevel.Normal, $"Retry scheduled for {job.DisplayTitle}: {ex.Message}", job.JobId);

                var delaySeconds = Math.Max(1, _settings.Queue.RetryDelaySeconds * job.RetryCount);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), queueCancellationToken);
            }
            else
            {
                job.Status = DownloadJobStatus.Failed;
                job.StatusMessage = "Failed";
                job.ErrorSummary = ex.Message;
                job.CompletedAt = DateTimeOffset.Now;
                await AppendHistoryAsync(job);
                _eventLog.Log(DownloaderLogLevel.ErrorsOnly, $"Failed: {job.DisplayTitle}. {ex.Message}", job.JobId);
            }
        }
        finally
        {
            stopwatch.Stop();
            _activeJobs.TryRemove(job.JobId, out _);
            RecomputeStatistics();
        }
    }

    private DownloadMediaProfile BuildMediaProfile(DownloadJob job)
    {
        return new DownloadMediaProfile
        {
            ProfileName = job.SelectedProfile,
            FormatExpression = string.IsNullOrWhiteSpace(job.SelectedProfile)
                ? _settings.Media.PreferredVideoFormat
                : job.SelectedProfile,
            AudioOnly = job.SelectedProfile.Contains("audio", StringComparison.OrdinalIgnoreCase),
            DownloadSubtitles = _settings.Media.DownloadSubtitles,
            DownloadThumbnail = _settings.Media.DownloadThumbnail,
            EmbedMetadata = _settings.Media.EmbedMetadata,
            AllowPlaylist = _settings.Media.AllowPlaylist,
            PreferredAudioFormat = _settings.Media.PreferredAudioFormat,
        };
    }

    private void ApplyProbe(DownloadJob job, DownloadProbeResult probe)
    {
        job.DisplayTitle = string.IsNullOrWhiteSpace(probe.DisplayTitle) ? job.SourceUrl : probe.DisplayTitle;
        job.ResolvedUrl = string.IsNullOrWhiteSpace(probe.ResolvedUrl) ? job.SourceUrl : probe.ResolvedUrl;
        job.TotalBytes = probe.TotalBytes;
        job.IsResumable = probe.SupportsResume;
        job.SegmentCount = Math.Clamp(probe.SuggestedSegments, 1, 8);
        job.SelectedProfile = string.IsNullOrWhiteSpace(probe.SelectedProfile) ? _settings.Media.PreferredVideoFormat : probe.SelectedProfile;

        if (!string.IsNullOrWhiteSpace(probe.SuggestedFileName))
        {
            job.RequestedFileName = probe.SuggestedFileName;
        }
    }

    private void ApplyProgress(DownloadJob job, DownloadProgressUpdate update)
    {
        if (update.Status.HasValue)
        {
            job.Status = update.Status.Value;
        }

        if (!string.IsNullOrWhiteSpace(update.StatusMessage))
        {
            job.StatusMessage = update.StatusMessage;
        }

        if (update.DownloadedBytes.HasValue)
        {
            job.DownloadedBytes = update.DownloadedBytes.Value;
        }

        if (update.TotalBytes.HasValue)
        {
            job.TotalBytes = update.TotalBytes;
        }

        if (update.SpeedBytesPerSecond.HasValue)
        {
            job.SpeedBytesPerSecond = update.SpeedBytesPerSecond.Value;
        }

        if (update.Eta.HasValue)
        {
            job.Eta = update.Eta;
        }

        if (update.ProgressPercent.HasValue)
        {
            job.ProgressPercent = update.ProgressPercent.Value;
        }

        if (update.ActiveSegments.HasValue)
        {
            job.ActiveSegments = update.ActiveSegments.Value;
        }

        if (!string.IsNullOrWhiteSpace(update.OutputFilePath))
        {
            job.OutputFilePath = update.OutputFilePath;
        }

        job.NotifyDerivedMetrics();
    }

    private async Task AppendHistoryAsync(DownloadJob job)
    {
        var entry = new DownloadHistoryEntry
        {
            JobId = job.JobId,
            SourceUrl = job.SourceUrl,
            Title = job.DisplayTitle,
            OutputFilePath = job.OutputFilePath,
            EngineType = job.EngineType,
            FinalStatus = job.Status,
            ErrorSummary = job.ErrorSummary,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            DownloadedBytes = job.DownloadedBytes,
            TotalBytes = job.TotalBytes,
            Category = job.Category,
        };

        await _historyService.AppendAsync(entry);
        await RunOnUiAsync(() => History.Insert(0, entry));
    }

    private void ResetJobForRetry(DownloadJob job)
    {
        job.Status = DownloadJobStatus.Queued;
        job.StatusMessage = "Queued";
        job.ProgressPercent = 0;
        job.DownloadedBytes = 0;
        job.SpeedBytesPerSecond = 0;
        job.Eta = null;
        job.ErrorSummary = string.Empty;
        job.CompletedAt = null;
        job.OutputFilePath = string.Empty;
        job.NotifyDerivedMetrics();
    }

    private void AttachToPackage(DownloadJob job)
    {
        var package = Packages.FirstOrDefault(item => item.PackageId == job.PackageId);
        if (package is null)
        {
            package = new DownloadPackage
            {
                PackageId = job.PackageId,
                Title = job.PackageTitle,
                OutputFolder = job.OutputDirectory,
            };

            Packages.Add(package);
        }

        package.AssetCount++;
    }

    private void RebuildPackages()
    {
        Packages.Clear();
        foreach (var grouped in Jobs.GroupBy(job => job.PackageId))
        {
            var first = grouped.First();
            Packages.Add(new DownloadPackage
            {
                PackageId = grouped.Key,
                Title = first.PackageTitle,
                OutputFolder = first.OutputDirectory,
                AssetCount = grouped.Count(),
                CompletedCount = grouped.Count(job => job.Status == DownloadJobStatus.Completed),
                ProgressPercent = grouped.Average(job => job.ProgressPercent),
            });
        }
    }

    private string BuildOutputDirectory(string categoryName, string? domain)
    {
        var directory = _settings.General.DefaultDownloadFolder;
        if (_settings.FileHandling.CreateCategorySubfolders)
        {
            directory = Path.Combine(directory, categoryName);
        }

        if (_settings.FileHandling.CreateDomainSubfolders && !string.IsNullOrWhiteSpace(domain))
        {
            directory = Path.Combine(directory, domain);
        }

        return directory;
    }

    private static string InferFileName(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "download.bin";
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        return string.IsNullOrWhiteSpace(fileName) ? "download.bin" : fileName;
    }

    private static string GetExtension(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return Path.GetExtension(uri.LocalPath);
    }

    private void OnScheduledActionTriggered(object? sender, DownloaderScheduledAction action)
    {
        if (action == DownloaderScheduledAction.StartQueue)
        {
            _ = StartQueueAsync();
        }
        else
        {
            _ = PauseQueueAsync();
        }
    }

    private static Task RunOnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private static async Task<T> RunOnUiAsync<T>(Func<T> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return action();
        }

        return await dispatcher.InvokeAsync(action);
    }

    private sealed class ActiveJobHandle
    {
        public ActiveJobHandle(CancellationTokenSource cancellation)
        {
            Cancellation = cancellation;
        }

        public CancellationTokenSource Cancellation { get; }

        public JobStopReason Reason { get; set; }
    }

    private enum JobStopReason
    {
        None,
        Pause,
        Cancel,
    }
}
