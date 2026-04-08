using System.Collections.ObjectModel;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

public interface IDownloaderSettingsService
{
    DownloaderSettings Load();

    void Save(DownloaderSettings settings);
}

public interface IDownloadInputParserService
{
    IReadOnlyList<string> ExtractCandidateUrls(string input);

    bool TryNormalizeUrl(string input, out string normalized);
}

public interface IDownloadCategoryService
{
    DownloadCategoryRule ResolveCategory(string url, string extension, DownloaderSettings settings);
}

public interface IDownloadEngine
{
    DownloadEngineType EngineType { get; }

    bool CanHandle(DownloadJob job, DownloaderSettings settings);

    Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken);

    Task ExecuteAsync(
        DownloadJob job,
        DownloaderSettings settings,
        DownloadExecutionPlan plan,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken);
}

public interface IDownloadEngineResolver
{
    Task<DownloadResolutionResult> ResolveAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken);
}

public interface IDownloadHistoryService
{
    Task<IReadOnlyList<DownloadHistoryEntry>> LoadAsync(CancellationToken cancellationToken = default);

    Task AppendAsync(DownloadHistoryEntry entry, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface IDownloadEventLogService
{
    event EventHandler<DownloadEventRecord>? EventRecorded;

    void Log(DownloaderLogLevel level, string message, Guid? jobId = null);

    Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default);
}

public interface IDownloadSchedulerService
{
    DateTimeOffset? ScheduledStartAt { get; }

    DateTimeOffset? ScheduledPauseAt { get; }

    event EventHandler<DownloaderScheduledAction>? ActionTriggered;

    void ScheduleStart(DateTimeOffset when);

    void SchedulePause(DateTimeOffset when);

    void Clear();
}

public interface IAssetDiscoveryService
{
    Task<IReadOnlyList<DownloadAssetCandidate>> DiscoverAsync(
        string url,
        bool deepCrawl,
        DownloaderSettings settings,
        IProgress<(int pages, int assets)>? progress,
        CancellationToken cancellationToken);
}

public interface IDownloadCoordinatorService
{
    ObservableCollection<DownloadJob> Jobs { get; }

    ObservableCollection<DownloadPackage> Packages { get; }

    ObservableCollection<DownloadHistoryEntry> History { get; }

    DownloadStatisticsSnapshot Statistics { get; }

    bool IsQueueRunning { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<int> AddFromInputAsync(string input, DownloaderMode mode, bool startImmediately, CancellationToken cancellationToken = default);

    Task<int> AddAssetsAsync(IEnumerable<DownloadAssetCandidate> assets, DownloaderMode mode, bool startImmediately, CancellationToken cancellationToken = default);

    Task StartQueueAsync(CancellationToken cancellationToken = default);

    Task PauseQueueAsync(CancellationToken cancellationToken = default);

    Task StopQueueAsync(CancellationToken cancellationToken = default);

    void PauseJobs(IEnumerable<DownloadJob> jobs);

    void ResumeJobs(IEnumerable<DownloadJob> jobs);

    void CancelJobs(IEnumerable<DownloadJob> jobs);

    void RetryJobs(IEnumerable<DownloadJob> jobs);

    void RemoveJobs(IEnumerable<DownloadJob> jobs);

    void ClearCompleted();

    void ClearFailed();

    void MoveJobsToTop(IEnumerable<DownloadJob> jobs);

    void MoveJobsToBottom(IEnumerable<DownloadJob> jobs);

    void RecomputeStatistics();

    void ReloadSettings();

    Task ClearHistoryAsync(CancellationToken cancellationToken = default);
}

public interface IDownloaderFileDialogService
{
    string? PickImportListFile();

    string? PickCookieFile();

    string? PickDiagnosticsExportPath();
}

public sealed class DownloadResolutionResult
{
    public IDownloadEngine Engine { get; init; } = null!;

    public DownloadProbeResult Probe { get; init; } = new();

    public List<string> Warnings { get; init; } = [];
}

public sealed class DownloadProbeResult
{
    public string DisplayTitle { get; set; } = string.Empty;

    public string ResolvedUrl { get; set; } = string.Empty;

    public long? TotalBytes { get; set; }

    public bool SupportsResume { get; set; }

    public int SuggestedSegments { get; set; } = 1;

    public string SuggestedFileName { get; set; } = string.Empty;

    public string SelectedProfile { get; set; } = string.Empty;
}

public sealed class DownloadExecutionPlan
{
    public string TargetDirectory { get; set; } = string.Empty;

    public string TargetFileName { get; set; } = string.Empty;

    public bool OverwriteExisting { get; set; }

    public bool AutoRenameExisting { get; set; } = true;

    public DownloadMediaProfile MediaProfile { get; set; } = new();
}

public sealed class DownloadProgressUpdate
{
    public DownloadJobStatus? Status { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public long? DownloadedBytes { get; set; }

    public long? TotalBytes { get; set; }

    public double? SpeedBytesPerSecond { get; set; }

    public TimeSpan? Eta { get; set; }

    public double? ProgressPercent { get; set; }

    public int? ActiveSegments { get; set; }

    public string OutputFilePath { get; set; } = string.Empty;
}
