namespace WindowsUtilityPack.Models;

/// <summary>Types of scraped web assets.</summary>
public enum AssetType
{
    Image,
    Video,
    Audio,
    Document,
    Archive,
    Executable,
    Font,
    Spreadsheet,
    Presentation,
    Database,
    Code,
    Text,
    Disk,
    Other,
}

/// <summary>
/// Represents a single downloadable asset discovered by the web scraper.
/// </summary>
public class ScrapedAsset
{
    /// <summary>Direct URL of the asset.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Suggested file name for saving.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Classified asset type.</summary>
    public AssetType Type { get; set; }

    /// <summary>MIME type hint derived from the URL or response headers.</summary>
    public string MimeHint { get; set; } = string.Empty;

    /// <summary>Whether the user has selected this asset for download.</summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>Resolution string for video/image assets (e.g. "1920x1080").</summary>
    public string Resolution { get; set; } = string.Empty;

    /// <summary>Estimated file size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>True if the asset is an HLS stream (.m3u8).</summary>
    public bool IsHls { get; set; }

    /// <summary>True if the asset is a DASH stream (.mpd).</summary>
    public bool IsDash { get; set; }

    /// <summary>Quality label (e.g. "1080p", "720p").</summary>
    public string Quality { get; set; } = string.Empty;

    /// <summary>File extension including the leading dot (e.g. ".pdf").</summary>
    public string FileExtension { get; set; } = string.Empty;

    /// <summary>URL of the page this asset was found on.</summary>
    public string SourcePageUrl { get; set; } = string.Empty;

    /// <summary>Human-readable label for <see cref="Type"/>.</summary>
    public string TypeLabel => Type.ToString();

    /// <summary>Display label showing the file extension in uppercase (e.g. "PDF").</summary>
    public string ExtensionLabel => string.IsNullOrEmpty(FileExtension)
        ? string.Empty
        : FileExtension.TrimStart('.').ToUpperInvariant();

    /// <summary>Human-readable file size label.</summary>
    public string SizeLabel => FileSizeBytes > 0 ? FormatBytes(FileSizeBytes) : string.Empty;

    private static string FormatBytes(long b) => b switch
    {
        >= 1L << 30 => $"{b / (1024.0 * 1024.0 * 1024.0):F2} GB",
        >= 1L << 20 => $"{b / (1024.0 * 1024.0):F1} MB",
        >= 1L << 10 => $"{b / 1024.0:F1} KB",
        _           => $"{b} B",
    };
}
