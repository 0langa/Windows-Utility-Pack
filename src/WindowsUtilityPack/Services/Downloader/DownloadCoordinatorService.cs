using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Coordinates queue lifecycle, engine execution, retries, history, and scheduler actions.</summary>
public sealed class DownloadCoordinatorService : IDownloadCoordinatorService, IDisposable
{
    private readonly IDownloadInputParserService _inputParser;
    private readonly IDownloadEngineResolver _engineResolver;
    private readonly IDownloadCategoryService _categoryService;
    private readonly IDownloaderSettingsService _settingsService;
    private readonly IDownloadHistoryService _historyService;
    private readonly IDownloadEventLogService _eventLog;
    private readonly IDownloadSchedulerService _scheduler;

    private readonly ConcurrentDictionary<Guid, ActiveJobHandle> _activeJobs = new();
    private readonly ConcurrentDictionary<Guid, Task> _runningTasks = new();
    private readonly object _sync = new();

    // Fix Issue 2: use int flag for atomic start guard (0=stopped, 1=running)
    private int _queueStarted;

    private DownloaderSettings _settings;
    private int _queueOrderSeed;
    private Task? _queueLoopTask;
    private CancellationTokenSource? _queueCts;
    private bool _disposed;

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

    public async Task<int> AddFromInputAsync(
        string input,
        DownloaderMode mode,
        bool startImmediately,
        bool probeOnAdd = true,
        CancellationToken cancellationToken = default)
    {
        if (mode is DownloaderMode.AssetGrabber or DownloaderMode.SiteCrawl)
        {
            _eventLog.Log(
                DownloaderLogLevel.Normal,
                "Discovery modes do not download directly. Use Scan Page or Crawl Site, then add discovered items.");
            return 0;
        }

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
                SelectedProfile = mode == DownloaderMode.MediaDownload
                    ? _settings.Media.PreferredVideoFormat
                    : "best",
                SelectedContainer = "mp4",
                MediaOutputKind = MediaOutputKind.Video,
                Status = _settings.General.StageLinksBeforeDownload && !startImmediately
                    ? DownloadJobStatus.Staged
                    : DownloadJobStatus.Queued,
                StatusMessage = _settings.General.StageLinksBeforeDownload && !startImmediately
                    ? "Staged"
                    : "Queued",
            };

            // Fix Issue 18: only probe during add when explicitly needed (not for batch asset adds)
            if (probeOnAdd)
            {
                var resolution = await _engineResolver.ResolveAsync(job, _settings, cancellationToken);
                ApplyProbe(job, resolution.Probe);
                job.EngineType = resolution.Engine.EngineType;
            }
            else
            {
                // Best-guess engine type without a network round-trip
                job.EngineType = DownloadEngineType.DirectHttp;
            }

            await RunOnUiAsync(() =>
            {
                Jobs.Add(job);
                AttachToPackage(job);
            });

            inserted++;
            _eventLog.Log(DownloaderLogLevel.Normal, $"Job added ({job.EngineType}): {job.SourceUrl}", job.JobId);
        }

        RecomputeStatistics();

        var shouldStart = startImmediately
            || (_settings.General.AutoStartOnAdd && mode == DownloaderMode.QuickDownload);
        if (shouldStart)
        {
            await StartQueueAsync(cancellationToken);
        }

        return inserted;
    }

    // IDownloadCoordinatorService signature without probeOnAdd (defaults to true)
    Task<int> IDownloadCoordinatorService.AddFromInputAsync(string input, DownloaderMode mode, bool startImmediately, CancellationToken cancellationToken)
        => AddFromInputAsync(input, mode, startImmediately, probeOnAdd: true, cancellationToken);

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
        var ingestionMode = mode is DownloaderMode.AssetGrabber or DownloaderMode.SiteCrawl
            ? DownloaderMode.QuickDownload
            : mode;

        // Fix Issue 18: skip probe-on-add for batch operations — the queue loop will probe at execution time
        foreach (var asset in unique)
        {
            if (await AddFromInputAsync(asset.Url, ingestionMode, startImmediately, probeOnAdd: false, cancellationToken) > 0)
            {
                added++;
            }
        }

        return added;
    }

    public Task StartQueueAsync(CancellationToken cancellationToken = default)
    {
        // Fix Issue 2: atomic check-and-set to prevent duplicate queue loops
        if (Interlocked.CompareExchange(ref _queueStarted, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        IsQueueRunning = true;
        _queueCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Do NOT pass _queueCts.Token as the Task.Run cancellation token — that would
        // cancel the task before it starts if Stop is called immediately. The loop
        // handles cancellation internally via QueueLoopAsync's try/catch.
        _queueLoopTask = Task.Run(() => QueueLoopAsync(_queueCts.Token));
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

        await WaitForRunningTasksAsync(cancellationToken);

        _eventLog.Log(DownloaderLogLevel.Normal, "Queue paused.");
    }

    public async Task StopQueueAsync(CancellationToken cancellationToken = default)
    {
        if (!IsQueueRunning && _activeJobs.IsEmpty)
        {
            return;
        }

        IsQueueRunning = false;

        var queueCts = _queueCts;
        if (queueCts is not null)
        {
            try
            {
                queueCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Queue loop may already have disposed the CTS during teardown.
            }
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

        await WaitForRunningTasksAsync(cancellationToken);

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
        if (selected.Count == 0 || Jobs.Count == 0)
        {
            return;
        }

        var baseOrder = Jobs.Min(job => job.QueueOrder) - selected.Count - 1;

        for (var i = 0; i < selected.Count; i++)
        {
            selected[i].QueueOrder = baseOrder + i;
        }

        NormalizeQueueOrder();
    }

    public void MoveJobsToBottom(IEnumerable<DownloadJob> jobs)
    {
        var selected = jobs.OrderBy(job => job.QueueOrder).ToList();
        if (selected.Count == 0 || Jobs.Count == 0)
        {
            return;
        }

        var baseOrder = Jobs.Max(job => job.QueueOrder) + 1;

        for (var i = 0; i < selected.Count; i++)
        {
            selected[i].QueueOrder = baseOrder + i;
        }

        NormalizeQueueOrder();
    }

    public void MoveJobsUp(IEnumerable<DownloadJob> jobs)
    {
        var selected = jobs.Distinct().OrderBy(job => job.QueueOrder).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var queue = Jobs.OrderBy(job => job.QueueOrder).ToList();
        foreach (var job in selected)
        {
            var index = queue.IndexOf(job);
            if (index <= 0)
            {
                continue;
            }

            (queue[index - 1], queue[index]) = (queue[index], queue[index - 1]);
        }

        ApplyQueueOrder(queue);
    }

    public void MoveJobsDown(IEnumerable<DownloadJob> jobs)
    {
        var selected = jobs.Distinct().OrderByDescending(job => job.QueueOrder).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var queue = Jobs.OrderBy(job => job.QueueOrder).ToList();
        foreach (var job in selected)
        {
            var index = queue.IndexOf(job);
            if (index < 0 || index >= queue.Count - 1)
            {
                continue;
            }

            (queue[index], queue[index + 1]) = (queue[index + 1], queue[index]);
        }

        ApplyQueueOrder(queue);
    }

    public void SetPriority(IEnumerable<DownloadJob> jobs, DownloadPriority priority)
    {
        foreach (var job in jobs.Distinct())
        {
            job.Priority = priority;
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

    // Fix Issue 4: thread-safe version for calls from background threads
    private Task RecomputeStatisticsAsync()
        => RunOnUiAsync(RecomputeStatistics);

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
                        handle.Dispose();
                        break;
                    }

                    var executionTask = Task.Run(() => ExecuteJobAsync(nextJob, handle, cancellationToken), CancellationToken.None);
                    _runningTasks[nextJob.JobId] = executionTask;
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
            // Fix Issue 2: reset the atomic flag so StartQueueAsync can be called again
            Interlocked.Exchange(ref _queueStarted, 0);
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

            // Fix Issue 13: only set Completed if the job wasn't already set to another terminal state
            // (guards against the narrow window where PauseJobs cancels a nearly-finished job)
            if (job.Status != DownloadJobStatus.Completed)
            {
                job.Status = DownloadJobStatus.Completed;
                job.ProgressPercent = 100;
                job.StatusMessage = "Completed";
                job.CompletedAt = DateTimeOffset.Now;
                job.NotifyDerivedMetrics();
            }

            await AppendHistoryAsync(job);
            _eventLog.Log(DownloaderLogLevel.Normal, $"Completed: {job.DisplayTitle}", job.JobId);
        }
        catch (OperationCanceledException)
        {
            // Fix Issue 13: don't overwrite a Completed status with Paused/Cancelled
            if (job.Status != DownloadJobStatus.Completed)
            {
                var reason = handle.Reason;
                job.Status = reason == JobStopReason.Pause ? DownloadJobStatus.Paused : DownloadJobStatus.Cancelled;
                job.StatusMessage = reason == JobStopReason.Pause ? "Paused" : "Cancelled";
                job.CompletedAt = DateTimeOffset.Now;

                if (reason != JobStopReason.Pause)
                {
                    await AppendHistoryAsync(job);
                }
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
                // Fix Issue 12: use linked token so per-job pause/cancel also aborts the retry delay
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), linked.Token);
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
            // Fix Issue 6: dispose the CancellationTokenSource held by the handle
            if (_activeJobs.TryRemove(job.JobId, out var removedHandle))
            {
                removedHandle.Dispose();
            }
            _runningTasks.TryRemove(job.JobId, out _);

            // Fix Issue 4: marshal statistics recomputation to the UI thread
            await RecomputeStatisticsAsync();
        }
    }

    private async Task WaitForRunningTasksAsync(CancellationToken cancellationToken)
    {
        var running = _runningTasks.Values
            .Where(task => !task.IsCompleted)
            .ToArray();

        if (running.Length == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(running).WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        catch (TimeoutException)
        {
            _eventLog.Log(DownloaderLogLevel.ErrorsOnly, "Timed out while waiting for in-flight download tasks to stop.");
        }
        catch (OperationCanceledException)
        {
            // Caller requested cancellation while waiting for shutdown.
        }
    }

    private DownloadMediaProfile BuildMediaProfile(DownloadJob job)
    {
        var audioOnly = job.MediaOutputKind == MediaOutputKind.AudioOnly;
        var formatExpression = string.IsNullOrWhiteSpace(job.SelectedProfile)
            ? (audioOnly ? "bestaudio/best" : _settings.Media.PreferredVideoFormat)
            : job.SelectedProfile;

        return new DownloadMediaProfile
        {
            ProfileName = job.SelectedProfile,
            FormatExpression = formatExpression,
            AudioOnly = audioOnly,
            DownloadSubtitles = _settings.Media.DownloadSubtitles,
            DownloadThumbnail = _settings.Media.DownloadThumbnail,
            EmbedMetadata = _settings.Media.EmbedMetadata,
            AllowPlaylist = _settings.Media.AllowPlaylist,
            PreferredAudioFormat = _settings.Media.PreferredAudioFormat,
            PreferredContainer = string.IsNullOrWhiteSpace(job.SelectedContainer) ? "mp4" : job.SelectedContainer,
        };
    }

    private void ApplyProbe(DownloadJob job, DownloadProbeResult probe)
    {
        job.DisplayTitle = string.IsNullOrWhiteSpace(probe.DisplayTitle) ? job.SourceUrl : probe.DisplayTitle;
        job.ResolvedUrl = string.IsNullOrWhiteSpace(probe.ResolvedUrl) ? job.SourceUrl : probe.ResolvedUrl;
        job.TotalBytes = probe.TotalBytes;
        job.IsResumable = probe.SupportsResume;
        job.SegmentCount = Math.Clamp(probe.SuggestedSegments, 1, 8);
        job.SelectedProfile = string.IsNullOrWhiteSpace(probe.SelectedProfile)
            ? (job.MediaOutputKind == MediaOutputKind.AudioOnly ? "bestaudio/best" : _settings.Media.PreferredVideoFormat)
            : probe.SelectedProfile;
        job.EffectivePlan = job.Mode == DownloaderMode.MediaDownload
            ? job.MediaOutputKind == MediaOutputKind.AudioOnly
                ? $"Audio only: {job.SelectedContainer.ToUpperInvariant()} ({_settings.Media.PreferredAudioFormat.ToUpperInvariant()})"
                : $"Video: {job.SelectedContainer.ToUpperInvariant()} ({job.SelectedProfile})"
            : $"Direct: {job.RequestedFileName}";

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

        // Fix Issue 15: sanitize the domain component before using it as a folder name
        if (_settings.FileHandling.CreateDomainSubfolders && !string.IsNullOrWhiteSpace(domain))
        {
            directory = Path.Combine(directory, SanitizeDomainFolderName(domain));
        }

        return directory;
    }

    /// <summary>
    /// Strips characters that are invalid in Windows file/directory names and
    /// removes leading dots to prevent relative path tricks (e.g. ".." traversal).
    /// </summary>
    private static string SanitizeDomainFolderName(string domain)
    {
        var sanitized = domain;
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(c, '_');
        }

        sanitized = sanitized.TrimStart('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "_domain" : sanitized;
    }

    private static string InferFileName(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"download-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.dat";
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        var host = uri.Host.Replace(".", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(host)
            ? $"download-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.dat"
            : $"{host}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.dat";
    }

    private static string GetExtension(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return Path.GetExtension(uri.LocalPath);
    }

    private static void ApplyQueueOrder(IReadOnlyList<DownloadJob> orderedJobs)
    {
        for (var i = 0; i < orderedJobs.Count; i++)
        {
            orderedJobs[i].QueueOrder = i + 1;
        }
    }

    private void NormalizeQueueOrder()
    {
        var ordered = Jobs.OrderBy(job => job.QueueOrder).ToList();
        ApplyQueueOrder(ordered);
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

    // Fix Issue 6: implement IDisposable so the CancellationTokenSource is properly released
    private sealed class ActiveJobHandle : IDisposable
    {
        public ActiveJobHandle(CancellationTokenSource cancellation)
        {
            Cancellation = cancellation;
        }

        public CancellationTokenSource Cancellation { get; }

        public JobStopReason Reason { get; set; }

        public void Dispose() => Cancellation.Dispose();
    }

    private enum JobStopReason
    {
        None,
        Pause,
        Cancel,
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scheduler.ActionTriggered -= OnScheduledActionTriggered;
        _ = StopQueueAsync();
    }
}
