namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

/// <summary>Persistent history entry for completed/failed downloader jobs.</summary>
public sealed class DownloadHistoryEntry
{
    public Guid JobId { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string OutputFilePath { get; set; } = string.Empty;

    public DownloadEngineType EngineType { get; set; }

    public DownloadJobStatus FinalStatus { get; set; }

    public string ErrorSummary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long DownloadedBytes { get; set; }

    public long? TotalBytes { get; set; }

    public string Category { get; set; } = string.Empty;
}
