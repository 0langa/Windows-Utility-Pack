using System.Net;
using System.Net.Http;
using WindowsUtilityPack.Services.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class WebScraperServiceTests
{
    [Fact]
    public async Task DownloadAsset_ExternalToolFailureIsNotReportedAsAnOutputFile()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wup-media-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        try
        {
            using var client = new HttpClient(new PageHandler());
            var service = new WebScraperService(new RejectingTool(), client);
            await Assert.ThrowsAsync<System.IO.IOException>(() => service.DownloadAssetAsync(new()
                { FileName = "fixture.m3u8", Url = "https://example.test/video.m3u8", IsHls = true }, folder, null));
            Assert.Empty(System.IO.Directory.GetFiles(folder));
        }
        finally { System.IO.Directory.Delete(folder, true); }
    }

    private sealed class RejectingTool : IDependencyManagerService
    {
        // The Windows where utility rejects ffmpeg's arguments without touching the network.
        public string FfmpegPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");
        public string YtDlpPath => "";
        public string GalleryDlPath => "";
        public DependencyStatus Check() => new(false, false, false);
        public Task EnsureAllAsync(Action<string> progress, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> UpdateYtDlpAsync(CancellationToken ct = default) => Task.FromResult("");
    }
    [Fact]
    public async Task Scrape_SelfReferencingIframeCannotRecurseForever()
    {
        using var handler = new PageHandler();
        using var client = new HttpClient(handler);
        var service = new WebScraperService(null!, client);
        var result = await service.ScrapeAsync("https://example.test/", false, 0, 1, null);
        Assert.Single(result);
        Assert.InRange(handler.Requests, 1, 2);
    }

    private sealed class PageHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (++Requests > 5) throw new HttpRequestException("Fixture recursion bound reached.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("<iframe src='/'>self</iframe><img src='/fixture.png'>") });
        }
    }
}
