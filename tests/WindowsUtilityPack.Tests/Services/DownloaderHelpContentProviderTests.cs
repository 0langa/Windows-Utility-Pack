using WindowsUtilityPack.Tools.NetworkInternet.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class DownloaderHelpContentProviderTests
{
    [Fact]
    public void Topics_AllHaveNonEmptyContent()
    {
        foreach (var topic in DownloaderHelpContentProvider.Topics)
        {
            var content = DownloaderHelpContentProvider.GetContent(topic);
            Assert.False(string.IsNullOrWhiteSpace(content));
        }
    }

    [Fact]
    public void GetContent_YouTubeTopicContainsExpectedGuidance()
    {
        var content = DownloaderHelpContentProvider.GetContent("YouTube quality tab");

        Assert.Contains("YouTube-only routing", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quality", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetContent_UnknownTopicReturnsFallback()
    {
        var content = DownloaderHelpContentProvider.GetContent("Unknown topic");

        Assert.Contains("No detailed guide is available", content, StringComparison.OrdinalIgnoreCase);
    }
}
