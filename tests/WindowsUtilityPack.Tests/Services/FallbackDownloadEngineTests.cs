using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Services.Downloader.Engines;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class FallbackDownloadEngineTests
{
    [Fact]
    public void CanHandle_ReturnsFalse_ForDiscoveryModes()
    {
        var fallback = new FallbackDownloadEngine(new DirectHttpDownloadEngine());
        var settings = new DownloaderSettings();

        var assetJob = new DownloadJob { SourceUrl = "https://example.com/file.zip", Mode = DownloaderMode.AssetGrabber };
        var crawlJob = new DownloadJob { SourceUrl = "https://example.com/file.zip", Mode = DownloaderMode.SiteCrawl };

        Assert.False(fallback.CanHandle(assetJob, settings));
        Assert.False(fallback.CanHandle(crawlJob, settings));
    }
}
