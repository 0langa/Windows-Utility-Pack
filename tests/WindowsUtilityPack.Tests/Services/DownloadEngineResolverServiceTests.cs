using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class DownloadEngineResolverServiceTests
{
    [Fact]
    public async Task ResolveAsync_Prefers_MediaEngine_ForMediaMode()
    {
        var media = new FakeEngine(DownloadEngineType.Media, canHandle: true);
        var direct = new FakeEngine(DownloadEngineType.DirectHttp, canHandle: true);
        var resolver = new DownloadEngineResolverService([direct, media]);

        var job = new DownloadJob
        {
            SourceUrl = "https://youtube.com/watch?v=1",
            Mode = DownloaderMode.MediaDownload,
        };

        var result = await resolver.ResolveAsync(job, new DownloaderSettings(), CancellationToken.None);

        Assert.Equal(DownloadEngineType.Media, result.Engine.EngineType);
    }

    [Fact]
    public async Task ResolveAsync_Returns_FallbackProbe_WhenProbeThrows()
    {
        var bad = new FakeEngine(DownloadEngineType.Media, canHandle: true, throwsOnProbe: true);
        var fallback = new FakeEngine(DownloadEngineType.Fallback, canHandle: true);
        var resolver = new DownloadEngineResolverService([bad, fallback]);

        var job = new DownloadJob { SourceUrl = "https://example.com/a.zip", Mode = DownloaderMode.QuickDownload };

        var result = await resolver.ResolveAsync(job, new DownloaderSettings(), CancellationToken.None);

        Assert.Equal(DownloadEngineType.Fallback, result.Engine.EngineType);
        Assert.NotEmpty(result.Warnings);
    }

    private sealed class FakeEngine : IDownloadEngine
    {
        private readonly bool _canHandle;
        private readonly bool _throwsOnProbe;

        public FakeEngine(DownloadEngineType engineType, bool canHandle, bool throwsOnProbe = false)
        {
            EngineType = engineType;
            _canHandle = canHandle;
            _throwsOnProbe = throwsOnProbe;
        }

        public DownloadEngineType EngineType { get; }

        public bool CanHandle(DownloadJob job, DownloaderSettings settings) => _canHandle;

        public Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
        {
            if (_throwsOnProbe)
            {
                throw new InvalidOperationException("probe failed");
            }

            return Task.FromResult(new DownloadProbeResult
            {
                DisplayTitle = "probe",
                SuggestedFileName = "a.bin",
                SuggestedSegments = 1,
                SupportsResume = false,
            });
        }

        public Task ExecuteAsync(DownloadJob job, DownloaderSettings settings, DownloadExecutionPlan plan, IProgress<DownloadProgressUpdate> progress, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
