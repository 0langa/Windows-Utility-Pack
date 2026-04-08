using System.IO;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Persisted settings for the premium downloader module.</summary>
public sealed class DownloaderSettings
{
    public DownloaderGeneralSettings General { get; set; } = new();

    public DownloaderQueueSettings Queue { get; set; } = new();

    public DownloaderConnectionSettings Connections { get; set; } = new();

    public DownloaderMediaSettings Media { get; set; } = new();

    public DownloaderScanSettings Scan { get; set; } = new();

    public DownloaderFileHandlingSettings FileHandling { get; set; } = new();

    public DownloaderLoggingSettings Logging { get; set; } = new();

    public DownloaderAdvancedSettings Advanced { get; set; } = new();

    public List<DownloadCategoryRule> Categories { get; set; } = DownloadCategoryRule.CreateDefaults();
}

public sealed class DownloaderGeneralSettings
{
    public bool AutoStartOnAdd { get; set; }

    public bool ClipboardMonitoring { get; set; }

    public bool StageLinksBeforeDownload { get; set; } = true;

    public string DefaultDownloadFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public DuplicateHandlingMode DuplicateHandlingMode { get; set; } = DuplicateHandlingMode.AutoRename;

    public bool CompactDensityMode { get; set; }
}

public sealed class DownloaderQueueSettings
{
    public int MaxConcurrentDownloads { get; set; } = 3;

    public int MaxRetries { get; set; } = 2;

    public int RetryDelaySeconds { get; set; } = 3;

    public DownloadPriority DefaultPriority { get; set; } = DownloadPriority.Normal;

    public bool StartNextAutomatically { get; set; } = true;

    public bool ContinueOnFailure { get; set; } = true;
}

public sealed class DownloaderConnectionSettings
{
    public int SegmentsPerDownload { get; set; } = 4;

    public int SegmentThresholdMb { get; set; } = 16;

    public int TimeoutSeconds { get; set; } = 45;

    public int MaxRedirects { get; set; } = 8;

    public int BandwidthLimitKbps { get; set; }

    public int PerHostConnectionLimit { get; set; } = 6;

    public string UserAgentOverride { get; set; } = string.Empty;

    public string CookieFilePath { get; set; } = string.Empty;

    public string CustomHeaders { get; set; } = string.Empty;
}

public sealed class DownloaderMediaSettings
{
    public string PreferredVideoFormat { get; set; } = "bestvideo+bestaudio/best";

    public string PreferredAudioFormat { get; set; } = "mp3";

    public bool DownloadSubtitles { get; set; }

    public bool DownloadThumbnail { get; set; }

    public bool EmbedMetadata { get; set; } = true;

    public bool AllowPlaylist { get; set; }

    public string OutputTemplate { get; set; } = "%(uploader)s/%(title)s.%(ext)s";

    public bool SaveCaptionFiles { get; set; }
}

public sealed class DownloaderScanSettings
{
    public bool SameDomainOnly { get; set; } = true;

    public bool SameSubPathOnly { get; set; } = true;

    public int MaxDepth { get; set; } = 2;

    public int MaxPages { get; set; } = 150;

    public int CrawlWorkers { get; set; } = 4;

    public bool ProbeContentType { get; set; } = true;

    public bool ScanScriptsAndJson { get; set; } = true;

    public bool UniqueAssetsOnly { get; set; } = true;

    public AssetFilterType DefaultFilter { get; set; } = AssetFilterType.All;
}

public sealed class DownloaderFileHandlingSettings
{
    public bool UsePartFiles { get; set; } = true;

    public bool ResumePartialFiles { get; set; } = true;

    public bool PreserveLastModifiedTimestamp { get; set; }

    public bool SanitizeFileNames { get; set; } = true;

    public bool CreateCategorySubfolders { get; set; } = true;

    public bool CreateDomainSubfolders { get; set; }

    public string FileNameTemplate { get; set; } = "{title}";
}

public sealed class DownloaderLoggingSettings
{
    public DownloaderLogLevel LogLevel { get; set; } = DownloaderLogLevel.Normal;

    public bool EnablePerJobVerboseLog { get; set; }

    public int KeepLogDays { get; set; } = 14;
}

public sealed class DownloaderAdvancedSettings
{
    public bool EnableDeveloperDiagnostics { get; set; }

    public bool ShowRawEngineCommandPreview { get; set; }

    public string CustomEngineArguments { get; set; } = string.Empty;

    public bool EnableExperimentalFeatures { get; set; }
}

/// <summary>Automatic category rule used for folder assignment and classification.</summary>
public sealed class DownloadCategoryRule
{
    public string Name { get; set; } = "General";

    public string DefaultFolder { get; set; } = string.Empty;

    public List<string> Extensions { get; set; } = [];

    public List<string> DomainContains { get; set; } = [];

    public DownloadPriority PriorityOverride { get; set; } = DownloadPriority.Normal;

    public static List<DownloadCategoryRule> CreateDefaults()
    {
        return
        [
            new DownloadCategoryRule
            {
                Name = "Videos",
                Extensions = [".mp4", ".mkv", ".webm", ".mov", ".m3u8", ".mpd"],
                DomainContains = ["youtube", "vimeo", "twitch"],
                PriorityOverride = DownloadPriority.High,
            },
            new DownloadCategoryRule
            {
                Name = "Audio",
                Extensions = [".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg"],
            },
            new DownloadCategoryRule
            {
                Name = "Images",
                Extensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg", ".bmp"],
            },
            new DownloadCategoryRule
            {
                Name = "Documents",
                Extensions = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md"],
            },
            new DownloadCategoryRule
            {
                Name = "Archives",
                Extensions = [".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz"],
            },
            new DownloadCategoryRule
            {
                Name = "Software",
                Extensions = [".exe", ".msi", ".apk", ".dmg", ".deb", ".rpm"],
            },
            new DownloadCategoryRule
            {
                Name = "Web Assets",
                Extensions = [".css", ".js", ".json", ".xml", ".woff", ".woff2", ".ttf", ".otf"],
            },
            new DownloadCategoryRule
            {
                Name = "Mixed",
                Extensions = [],
            },
        ];
    }
}
