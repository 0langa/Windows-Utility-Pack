namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

/// <summary>Acquisition mode selected by the user for new links.</summary>
public enum DownloaderMode
{
    QuickDownload,
    MediaDownload,
    AssetGrabber,
    SiteCrawl,
}

/// <summary>Resolved engine type used to process a download job.</summary>
public enum DownloadEngineType
{
    DirectHttp,
    Media,
    Gallery,
    AssetScraper,
    Fallback,
}

/// <summary>Lifecycle status for a download job.</summary>
public enum DownloadJobStatus
{
    Staged,
    Queued,
    Probing,
    Downloading,
    Processing,
    Paused,
    Completed,
    Failed,
    Cancelled,
    Skipped,
}

/// <summary>Queue priority used when selecting the next job to run.</summary>
public enum DownloadPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
}

/// <summary>Duplicate handling mode for queue ingestion and file writes.</summary>
public enum DuplicateHandlingMode
{
    Skip,
    AutoRename,
    Overwrite,
}

/// <summary>Downloader diagnostics verbosity.</summary>
public enum DownloaderLogLevel
{
    Off,
    ErrorsOnly,
    Normal,
    Verbose,
}

/// <summary>Type filter used in asset discovery views.</summary>
public enum AssetFilterType
{
    All,
    Images,
    Video,
    Audio,
    Archives,
    Documents,
    Executables,
    CodeTextData,
    Fonts,
}

/// <summary>Planned scheduler action for queue automation.</summary>
public enum DownloaderScheduledAction
{
    StartQueue,
    PauseQueue,
}