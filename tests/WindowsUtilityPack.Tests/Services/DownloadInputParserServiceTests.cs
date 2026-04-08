using WindowsUtilityPack.Services.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class DownloadInputParserServiceTests
{
    [Fact]
    public void ExtractCandidateUrls_Deduplicates_And_Normalizes_WwwLinks()
    {
        var parser = new DownloadInputParserService();

        var urls = parser.ExtractCandidateUrls("www.example.com/file.zip www.example.com/file.zip");

        Assert.Single(urls);
        Assert.Equal("https://www.example.com/file.zip", urls[0]);
    }

    [Fact]
    public void TryNormalizeUrl_Rejects_NonHttpScheme()
    {
        var parser = new DownloadInputParserService();

        var ok = parser.TryNormalizeUrl("ftp://example.com/a.zip", out var normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void ExtractCandidateUrls_Handles_Multiline_Noisy_Text()
    {
        var parser = new DownloadInputParserService();

        var urls = parser.ExtractCandidateUrls("Download this: https://example.com/a\nAnd this one -> www.contoso.com/b");

        Assert.Equal(2, urls.Count);
        Assert.Contains("https://example.com/a", urls);
        Assert.Contains("https://www.contoso.com/b", urls);
    }
}
