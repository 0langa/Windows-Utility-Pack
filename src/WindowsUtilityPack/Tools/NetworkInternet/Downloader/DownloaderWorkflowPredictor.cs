using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader;

/// <summary>
/// Predicts the user-facing workflow label for a URL in the current mode.
/// </summary>
public static class DownloaderWorkflowPredictor
{
    public static string Predict(string url, DownloaderMode mode)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "Fallback direct";
        }

        if (mode == DownloaderMode.MediaDownload)
        {
            return "Media extraction";
        }

        var host = uri.Host;
        if (DownloaderKnownHosts.Matches(host, DownloaderKnownHosts.MediaHosts))
        {
            return "Media extraction";
        }

        if (DownloaderKnownHosts.Matches(host, DownloaderKnownHosts.GalleryHosts))
        {
            return "Gallery/collection";
        }

        return mode switch
        {
            DownloaderMode.AssetGrabber => "Asset scan",
            DownloaderMode.SiteCrawl => "Site crawl",
            _ => "Direct file",
        };
    }
}
