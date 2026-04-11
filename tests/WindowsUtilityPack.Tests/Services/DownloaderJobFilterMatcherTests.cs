using WindowsUtilityPack.Tools.NetworkInternet.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class DownloaderJobFilterMatcherTests
{
    [Fact]
    public void Matches_StatusFilterAll_AndEmptyQuery_ReturnsTrue()
    {
        var job = CreateJob();

        var result = DownloaderJobFilterMatcher.Matches(job, string.Empty, "All");

        Assert.True(result);
    }

    [Fact]
    public void Matches_StatusFilterMismatch_ReturnsFalse()
    {
        var job = CreateJob();
        job.Status = DownloadJobStatus.Completed;

        var result = DownloaderJobFilterMatcher.Matches(job, string.Empty, "Queued");

        Assert.False(result);
    }

    [Fact]
    public void Matches_QueryMatchesPlanOrTitle_ReturnsTrue()
    {
        var job = CreateJob();
        job.DisplayTitle = "Ubuntu ISO";
        job.EffectivePlan = "Direct: ubuntu.iso";

        Assert.True(DownloaderJobFilterMatcher.Matches(job, "ubuntu", "All"));
        Assert.True(DownloaderJobFilterMatcher.Matches(job, "direct", "All"));
    }

    [Fact]
    public void Matches_QueryNotFound_ReturnsFalse()
    {
        var job = CreateJob();

        var result = DownloaderJobFilterMatcher.Matches(job, "nonexistent", "All");

        Assert.False(result);
    }

    private static DownloadJob CreateJob()
    {
        return new DownloadJob
        {
            SourceUrl = "https://example.com/file.zip",
            DisplayTitle = "file.zip",
            Category = "General",
            EngineType = DownloadEngineType.DirectHttp,
            Status = DownloadJobStatus.Queued,
            EffectivePlan = "Direct: file.zip",
        };
    }
}
