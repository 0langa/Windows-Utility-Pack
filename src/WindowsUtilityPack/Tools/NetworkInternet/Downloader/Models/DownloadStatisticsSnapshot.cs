namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

/// <summary>Live queue counters shown in downloader dashboards and headers.</summary>
public sealed class DownloadStatisticsSnapshot
{
    public int Queued { get; set; }

    public int Active { get; set; }

    public int Paused { get; set; }

    public int Completed { get; set; }

    public int Failed { get; set; }

    public int Skipped { get; set; }

    public int Cancelled { get; set; }

    public int Total => Queued + Active + Paused + Completed + Failed + Skipped + Cancelled;
}
