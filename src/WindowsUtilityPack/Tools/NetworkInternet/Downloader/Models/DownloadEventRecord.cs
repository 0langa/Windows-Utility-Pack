namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

/// <summary>Runtime event emitted by downloader services for diagnostics and status strip visibility.</summary>
public sealed class DownloadEventRecord
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    public DownloaderLogLevel Level { get; set; } = DownloaderLogLevel.Normal;

    public Guid? JobId { get; set; }

    public string Message { get; set; } = string.Empty;
}
