using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

/// <summary>Represents one download job inside the queue manager.</summary>
public sealed class DownloadJob : ViewModelBase
{
    private DownloadJobStatus _status = DownloadJobStatus.Staged;
    private double _progressPercent;
    private long _downloadedBytes;
    private long? _totalBytes;
    private double _speedBytesPerSecond;
    private TimeSpan? _eta;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _completedAt;
    private int _retryCount;
    private int _activeSegments;
    private string _statusMessage = "Staged";
    private string _displayTitle = string.Empty;
    private string _resolvedUrl = string.Empty;
    private string _errorSummary = string.Empty;
    private string _outputFilePath = string.Empty;
    private DownloadPriority _priority = DownloadPriority.Normal;
    private MediaOutputKind _mediaOutputKind = MediaOutputKind.Video;
    private string _effectivePlan = string.Empty;
    private int _queueOrder;

    public Guid JobId { get; init; } = Guid.NewGuid();

    public string SourceUrl { get; init; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string PackageTitle { get; set; } = string.Empty;

    public string Category { get; set; } = "Mixed";

    public DownloaderMode Mode { get; set; } = DownloaderMode.QuickDownload;

    public DownloadEngineType EngineType { get; set; } = DownloadEngineType.Fallback;

    public string OutputDirectory { get; set; } = string.Empty;

    public string RequestedFileName { get; set; } = string.Empty;

    public string SelectedProfile { get; set; } = "best";

    public string SelectedContainer { get; set; } = "mp4";

    public bool IsResumable { get; set; }

    public int SegmentCount { get; set; } = 1;

    public string AuthenticationProfile { get; set; } = string.Empty;

    public string DetailedLogPath { get; set; } = string.Empty;

    public int QueueOrder
    {
        get => _queueOrder;
        set => SetProperty(ref _queueOrder, value);
    }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DownloadJobStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, Math.Clamp(value, 0, 100));
    }

    public long DownloadedBytes
    {
        get => _downloadedBytes;
        set => SetProperty(ref _downloadedBytes, value);
    }

    public long? TotalBytes
    {
        get => _totalBytes;
        set => SetProperty(ref _totalBytes, value);
    }

    public double SpeedBytesPerSecond
    {
        get => _speedBytesPerSecond;
        set => SetProperty(ref _speedBytesPerSecond, Math.Max(0, value));
    }

    public TimeSpan? Eta
    {
        get => _eta;
        set => SetProperty(ref _eta, value);
    }

    public DateTimeOffset? StartedAt
    {
        get => _startedAt;
        set => SetProperty(ref _startedAt, value);
    }

    public DateTimeOffset? CompletedAt
    {
        get => _completedAt;
        set => SetProperty(ref _completedAt, value);
    }

    public int RetryCount
    {
        get => _retryCount;
        set => SetProperty(ref _retryCount, Math.Max(0, value));
    }

    public int ActiveSegments
    {
        get => _activeSegments;
        set => SetProperty(ref _activeSegments, Math.Max(0, value));
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value ?? string.Empty);
    }

    public string DisplayTitle
    {
        get => _displayTitle;
        set => SetProperty(ref _displayTitle, value ?? string.Empty);
    }

    public string ResolvedUrl
    {
        get => _resolvedUrl;
        set => SetProperty(ref _resolvedUrl, value ?? string.Empty);
    }

    public string ErrorSummary
    {
        get => _errorSummary;
        set => SetProperty(ref _errorSummary, value ?? string.Empty);
    }

    public string OutputFilePath
    {
        get => _outputFilePath;
        set => SetProperty(ref _outputFilePath, value ?? string.Empty);
    }

    public DownloadPriority Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    public MediaOutputKind MediaOutputKind
    {
        get => _mediaOutputKind;
        set => SetProperty(ref _mediaOutputKind, value);
    }

    public string EffectivePlan
    {
        get => _effectivePlan;
        set => SetProperty(ref _effectivePlan, value ?? string.Empty);
    }

    public string ProgressLabel => TotalBytes is > 0
        ? $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes.Value)}"
        : FormatBytes(DownloadedBytes);

    public string SpeedLabel => SpeedBytesPerSecond <= 0
        ? string.Empty
        : $"{FormatBytes((long)SpeedBytesPerSecond)}/s";

    public string EtaLabel => Eta is null
        ? string.Empty
        : Eta.Value.TotalHours >= 1
            ? Eta.Value.ToString("hh\\:mm\\:ss")
            : Eta.Value.ToString("mm\\:ss");

    public void NotifyDerivedMetrics()
    {
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(SpeedLabel));
        OnPropertyChanged(nameof(EtaLabel));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        return bytes switch
        {
            >= 1L << 30 => $"{bytes / (1024d * 1024d * 1024d):F2} GB",
            >= 1L << 20 => $"{bytes / (1024d * 1024d):F1} MB",
            >= 1L << 10 => $"{bytes / 1024d:F1} KB",
            _ => $"{bytes} B",
        };
    }
}
