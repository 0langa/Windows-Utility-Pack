using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader;

/// <summary>
/// Central matcher used by queue filtering so behavior is testable and consistent.
/// </summary>
public static class DownloaderJobFilterMatcher
{
    public static bool Matches(DownloadJob job, string? query, string? statusFilter)
    {
        if (job is null)
        {
            return false;
        }

        var effectiveStatus = string.IsNullOrWhiteSpace(statusFilter) ? "All" : statusFilter.Trim();
        if (!string.Equals(effectiveStatus, "All", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(job.Status.ToString(), effectiveStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var text = query.Trim();
        return (job.DisplayTitle?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
            || (job.SourceUrl?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
            || (job.Category?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
            || job.Status.ToString().Contains(text, StringComparison.OrdinalIgnoreCase)
            || job.EngineType.ToString().Contains(text, StringComparison.OrdinalIgnoreCase)
            || (job.EffectivePlan?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
