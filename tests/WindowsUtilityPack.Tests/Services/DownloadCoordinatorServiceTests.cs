using System.Collections.ObjectModel;
using System.IO;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class DownloadCoordinatorServiceTests
{
    [Fact]
    public async Task AddFromInputAsync_Rejects_DiscoveryModes()
    {
        using var scheduler = new DownloadSchedulerService();
        var coordinator = CreateCoordinator(scheduler);

        var assetResult = await coordinator.AddFromInputAsync("https://example.com/file.zip", DownloaderMode.AssetGrabber, false);
        var crawlResult = await coordinator.AddFromInputAsync("https://example.com/file.zip", DownloaderMode.SiteCrawl, false);

        Assert.Equal(0, assetResult);
        Assert.Equal(0, crawlResult);
        Assert.Empty(coordinator.Jobs);
    }

    [Fact]
    public async Task AddFromInputAsync_MediaMode_Defaults_ToVideoOutput()
    {
        using var scheduler = new DownloadSchedulerService();
        var coordinator = CreateCoordinator(scheduler);

        var added = await coordinator.AddFromInputAsync("https://youtube.com/watch?v=test", DownloaderMode.MediaDownload, false);

        var job = Assert.Single(coordinator.Jobs);
        Assert.Equal(1, added);
        Assert.Equal(DownloaderMode.MediaDownload, job.Mode);
        Assert.Equal(MediaOutputKind.Video, job.MediaOutputKind);
        Assert.Equal("bestvideo+bestaudio/best", job.SelectedProfile);
    }

    [Fact]
    public async Task AddAssetsAsync_DiscoveryAdds_QuickDownloadJobs()
    {
        using var scheduler = new DownloadSchedulerService();
        var coordinator = CreateCoordinator(scheduler);
        var assets = new[]
        {
            new DownloadAssetCandidate
            {
                Url = "https://cdn.example.com/picture.jpg",
                Name = "picture.jpg",
                IsSelected = true,
            },
        };

        var added = await coordinator.AddAssetsAsync(assets, DownloaderMode.AssetGrabber, false);

        var job = Assert.Single(coordinator.Jobs);
        Assert.Equal(1, added);
        Assert.Equal(DownloaderMode.QuickDownload, job.Mode);
    }

    private static DownloadCoordinatorService CreateCoordinator(IDownloadSchedulerService scheduler)
    {
        return new DownloadCoordinatorService(
            new DownloadInputParserService(),
            new FakeResolver(),
            new DownloadCategoryService(),
            new FakeSettingsService(),
            new FakeHistoryService(),
            new FakeEventLogService(),
            scheduler);
    }

    private sealed class FakeResolver : IDownloadEngineResolver
    {
        private readonly IDownloadEngine _engine = new FakeEngine();

        public Task<DownloadResolutionResult> ResolveAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DownloadResolutionResult
            {
                Engine = _engine,
                Probe = new DownloadProbeResult
                {
                    DisplayTitle = "probe",
                    ResolvedUrl = job.SourceUrl,
                    SuggestedFileName = "file.dat",
                    SupportsResume = false,
                    SuggestedSegments = 1,
                    SelectedProfile = string.Empty,
                },
            });
        }
    }

    private sealed class FakeEngine : IDownloadEngine
    {
        public DownloadEngineType EngineType => DownloadEngineType.DirectHttp;

        public bool CanHandle(DownloadJob job, DownloaderSettings settings) => true;

        public Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DownloadProbeResult());
        }

        public Task ExecuteAsync(DownloadJob job, DownloaderSettings settings, DownloadExecutionPlan plan, IProgress<DownloadProgressUpdate> progress, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsService : IDownloaderSettingsService
    {
        private DownloaderSettings _settings = new();

        public DownloaderSettings Load() => _settings;

        public void Save(DownloaderSettings settings) => _settings = settings;
    }

    private sealed class FakeHistoryService : IDownloadHistoryService
    {
        public Task<IReadOnlyList<DownloadHistoryEntry>> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DownloadHistoryEntry>>([]);
        }

        public Task AppendAsync(DownloadHistoryEntry entry, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventLogService : IDownloadEventLogService
    {
        public event EventHandler<DownloadEventRecord>? EventRecorded;

        public void Log(DownloaderLogLevel level, string message, Guid? jobId = null)
        {
            EventRecorded?.Invoke(this, new DownloadEventRecord
            {
                Level = level,
                Message = message,
                JobId = jobId,
            });
        }

        public Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Path.GetTempFileName());
        }
    }
}
