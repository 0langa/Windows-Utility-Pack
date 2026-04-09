using WindowsUtilityPack.Services.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class YouTubeDownloadPlanBuilderTests
{
    [Fact]
    public void BuildFormatExpression_Applies_Resolution_Fps_And_CodecFilters()
    {
        var options = new YouTubeDownloadOptions
        {
            VideoQuality = "1080p (Full HD)",
            FrameRate = "Up to 30 fps",
            VideoCodec = "H.264 (AVC)",
            AudioQuality = "Balanced (~192 kbps)",
            AudioCodec = "Opus",
            Container = "mp4",
        };

        var expression = YouTubeDownloadPlanBuilder.BuildFormatExpression(options);

        Assert.Contains("height<=1080", expression, StringComparison.Ordinal);
        Assert.Contains("fps<=30", expression, StringComparison.Ordinal);
        Assert.Contains("vcodec*=avc1", expression, StringComparison.Ordinal);
        Assert.Contains("abr<=192", expression, StringComparison.Ordinal);
        Assert.Contains("acodec*=opus", expression, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc", true)]
    [InlineData("https://youtu.be/abc", true)]
    [InlineData("https://example.com/video", false)]
    [InlineData("not-a-url", false)]
    public void IsYouTubeUrl_Detects_ExpectedHosts(string value, bool expected)
    {
        Assert.Equal(expected, YouTubeDownloadPlanBuilder.IsYouTubeUrl(value));
    }
}
