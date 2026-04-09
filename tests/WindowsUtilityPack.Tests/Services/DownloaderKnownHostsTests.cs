using WindowsUtilityPack.Services.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>Tests for the centralised known-host helper.</summary>
public class DownloaderKnownHostsTests
{
    [Theory]
    [InlineData("www.youtube.com")]
    [InlineData("youtu.be")]
    [InlineData("m.youtube.com")]
    [InlineData("www.vimeo.com")]
    [InlineData("www.dailymotion.com")]
    [InlineData("clips.twitch.tv")]
    [InlineData("soundcloud.com")]
    public void Matches_ReturnsTrue_ForMediaHosts(string host)
    {
        Assert.True(DownloaderKnownHosts.Matches(host, DownloaderKnownHosts.MediaHosts));
    }

    [Theory]
    [InlineData("www.imgur.com")]
    [InlineData("old.reddit.com")]
    [InlineData("www.flickr.com")]
    [InlineData("www.deviantart.com")]
    [InlineData("www.pixiv.net")]
    [InlineData("someuser.tumblr.com")]
    public void Matches_ReturnsTrue_ForGalleryHosts(string host)
    {
        Assert.True(DownloaderKnownHosts.Matches(host, DownloaderKnownHosts.GalleryHosts));
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("cdn.example.net")]
    [InlineData("files.github.com")]
    public void Matches_ReturnsFalse_ForUnknownHosts(string host)
    {
        Assert.False(DownloaderKnownHosts.Matches(host, DownloaderKnownHosts.MediaHosts));
        Assert.False(DownloaderKnownHosts.Matches(host, DownloaderKnownHosts.GalleryHosts));
    }

    [Fact]
    public void MediaHosts_And_GalleryHosts_Are_Disjoint()
    {
        // No host should appear in both lists
        var overlap = DownloaderKnownHosts.MediaHosts
            .Where(m => DownloaderKnownHosts.GalleryHosts
                .Any(g => g.Equals(m, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(overlap);
    }
}
