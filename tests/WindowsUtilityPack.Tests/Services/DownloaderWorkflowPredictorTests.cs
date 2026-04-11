using WindowsUtilityPack.Tools.NetworkInternet.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class DownloaderWorkflowPredictorTests
{
    [Theory]
    [InlineData("not-a-url", DownloaderMode.QuickDownload, "Fallback direct")]
    [InlineData("https://youtube.com/watch?v=1", DownloaderMode.QuickDownload, "Media extraction")]
    [InlineData("https://imgur.com/a/xyz", DownloaderMode.QuickDownload, "Gallery/collection")]
    [InlineData("https://example.com/page", DownloaderMode.AssetGrabber, "Asset scan")]
    [InlineData("https://example.com/page", DownloaderMode.SiteCrawl, "Site crawl")]
    [InlineData("https://example.com/file.zip", DownloaderMode.QuickDownload, "Direct file")]
    public void Predict_ReturnsExpectedWorkflow(string url, DownloaderMode mode, string expected)
    {
        var result = DownloaderWorkflowPredictor.Predict(url, mode);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Predict_MediaMode_AlwaysReturnsMediaExtraction()
    {
        var result = DownloaderWorkflowPredictor.Predict("https://example.com/video", DownloaderMode.MediaDownload);
        Assert.Equal("Media extraction", result);
    }
}
