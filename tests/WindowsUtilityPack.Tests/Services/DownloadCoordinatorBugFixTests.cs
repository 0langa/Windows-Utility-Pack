using System.Collections.ObjectModel;
using System.IO;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>Regression tests for bugs fixed in the coordinator.</summary>
public class DownloadCoordinatorBugFixTests
{
    // ── Issue 2: StartQueueAsync is idempotent ───────────────────────────────

    [Fact]
    public async Task StartQueueAsync_CalledTwiceConcurrently_DoesNotDeadlock()
    {
        using var scheduler = new DownloadSchedulerService();
        var coordinator = CreateCoordinator(scheduler);

        // Both calls must complete without hanging or throwing
        var t1 = coordinator.StartQueueAsync();
        var t2 = coordinator.StartQueueAsync();
        await Task.WhenAll(t1, t2);

        // Stop is also safe even if the queue already drained
        await coordinator.StopQueueAsync();
        Assert.False(coordinator.IsQueueRunning);
    }

    [Fact]
    public async Task StartQueueAsync_AfterStop_CanRestartQueue()
    {
        using var scheduler = new DownloadSchedulerService();
        var coordinator = CreateCoordinator(scheduler);

        await coordinator.StartQueueAsync();
        await coordinator.StopQueueAsync();
        Assert.False(coordinator.IsQueueRunning);

        // Must be able to start again after a stop
        await coordinator.StartQueueAsync();
        await coordinator.StopQueueAsync();
        Assert.False(coordinator.IsQueueRunning);
    }

    // ── Issue 13: PauseJobs does not overwrite Completed status ─────────────

    [Fact]
    public async Task PauseJobs_DoesNotRevert_CompletedJob()
    {
        using var scheduler = new DownloadSchedulerService();
        var slowResolver = new InstantCompletingResolver();
        var coordinator = CreateCoordinator(scheduler, slowResolver);

        var added = await coordinator.AddFromInputAsync(
            "https://example.com/file.zip", DownloaderMode.QuickDownload, startImmediately: true);
        Assert.Equal(1, added);

        // Give the queue loop time to finish the (instant) job
        await Task.Delay(500);

        var job = Assert.Single(coordinator.Jobs);
        Assert.Equal(DownloadJobStatus.Completed, job.Status);

        // Attempting to pause a completed job should be a no-op
        coordinator.PauseJobs([job]);

        Assert.Equal(DownloadJobStatus.Completed, job.Status);
    }

    // ── Issue 15: domain folder names are sanitised ──────────────────────────

    [Fact]
    public async Task AddFromInputAsync_CreatesDomainSubfolder_ForValidHost()
    {
        using var scheduler = new DownloadSchedulerService();
        var settings = new DownloaderSettings();
        settings.FileHandling.CreateDomainSubfolders = true;
        var coordinator = CreateCoordinator(scheduler, settingsService: new FakeSettingsService(settings));

        await coordinator.AddFromInputAsync("https://example.com/file.zip", DownloaderMode.QuickDownload, false);

        var job = Assert.Single(coordinator.Jobs);
        Assert.Contains("example.com", job.OutputDirectory);
        // Path must not contain a directory traversal sequence
        Assert.DoesNotContain("..", job.OutputDirectory);
    }

    [Fact]
    public async Task AddFromInputAsync_OutputDirectory_ContainsNoPathTraversal()
    {
        // Even with a valid URL, the resulting output path must never contain ".."
        using var scheduler = new DownloadSchedulerService();
        var settings = new DownloaderSettings();
        settings.FileHandling.CreateDomainSubfolders = true;
        settings.General.DefaultDownloadFolder = "C:\\Downloads";
        var coordinator = CreateCoordinator(scheduler, settingsService: new FakeSettingsService(settings));

        await coordinator.AddFromInputAsync("https://example.com/file.zip", DownloaderMode.QuickDownload, false);

        var job = Assert.Single(coordinator.Jobs);
        Assert.DoesNotContain("..", job.OutputDirectory);
    }

    // ── Issue 18: AddAssetsAsync skips per-asset probe ──────────────────────

    [Fact]
    public async Task AddAssetsAsync_DoesNotProbe_ForBatchAssets()
    {
        using var scheduler = new DownloadSchedulerService();
        var countingResolver = new CountingResolver();
        var coordinator = CreateCoordinator(scheduler, countingResolver);

        var assets = new[]
        {
            new DownloadAssetCandidate { Url = "https://cdn.example.com/a.jpg", IsSelected = true },
            new DownloadAssetCandidate { Url = "https://cdn.example.com/b.jpg", IsSelected = true },
            new DownloadAssetCandidate { Url = "https://cdn.example.com/c.jpg", IsSelected = true },
        };

        await coordinator.AddAssetsAsync(assets, DownloaderMode.AssetGrabber, false);

        Assert.Equal(3, coordinator.Jobs.Count);
        // probeOnAdd=false → ResolveAsync should NOT be called during add
        Assert.Equal(0, countingResolver.ResolveCallCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DownloadCoordinatorService CreateCoordinator(
        IDownloadSchedulerService scheduler,
        IDownloadEngineResolver? resolver = null,
        IDownloaderSettingsService? settingsService = null)
    {
        return new DownloadCoordinatorService(
            new DownloadInputParserService(),
            resolver ?? new FakeResolver(),
            new DownloadCategoryService(),
            settingsService ?? new FakeSettingsService(new DownloaderSettings()),
            new FakeHistoryService(),
            new FakeEventLogService(),
            scheduler);
    }

    // Resolver that completes instantly and counts calls
    private sealed class CountingResolver : IDownloadEngineResolver
    {
        public int ResolveCallCount;

        public Task<DownloadResolutionResult> ResolveAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ResolveCallCount);
            return Task.FromResult(new DownloadResolutionResult
            {
                Engine = new FakeEngine(),
                Probe = new DownloadProbeResult { ResolvedUrl = job.SourceUrl, SuggestedFileName = "file.dat" },
            });
        }
    }

    // Resolver whose engine completes execution immediately
    private sealed class InstantCompletingResolver : IDownloadEngineResolver
    {
        public Task<DownloadResolutionResult> ResolveAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DownloadResolutionResult
            {
                Engine = new InstantEngine(),
                Probe = new DownloadProbeResult { ResolvedUrl = job.SourceUrl, SuggestedFileName = "file.dat" },
            });
        }
    }

    private sealed class InstantEngine : IDownloadEngine
    {
        public DownloadEngineType EngineType => DownloadEngineType.DirectHttp;
        public bool CanHandle(DownloadJob job, DownloaderSettings settings) => true;
        public Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken ct)
            => Task.FromResult(new DownloadProbeResult());
        public Task ExecuteAsync(DownloadJob job, DownloaderSettings settings, DownloadExecutionPlan plan, IProgress<DownloadProgressUpdate> progress, CancellationToken ct)
        {
            progress.Report(new DownloadProgressUpdate
            {
                Status = DownloadJobStatus.Completed,
                ProgressPercent = 100,
                StatusMessage = "Done",
            });
            return Task.CompletedTask;
        }
    }

    private sealed class FakeResolver : IDownloadEngineResolver
    {
        public Task<DownloadResolutionResult> ResolveAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DownloadResolutionResult
            {
                Engine = new FakeEngine(),
                Probe = new DownloadProbeResult { ResolvedUrl = job.SourceUrl, SuggestedFileName = "file.dat" },
            });
        }
    }

    private sealed class FakeEngine : IDownloadEngine
    {
        public DownloadEngineType EngineType => DownloadEngineType.DirectHttp;
        public bool CanHandle(DownloadJob job, DownloaderSettings settings) => true;
        public Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken ct)
            => Task.FromResult(new DownloadProbeResult());
        public Task ExecuteAsync(DownloadJob job, DownloaderSettings settings, DownloadExecutionPlan plan, IProgress<DownloadProgressUpdate> progress, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FakeSettingsService : IDownloaderSettingsService
    {
        private DownloaderSettings _settings;
        public FakeSettingsService(DownloaderSettings settings) => _settings = settings;
        public DownloaderSettings Load() => _settings;
        public void Save(DownloaderSettings settings) => _settings = settings;
    }

    private sealed class FakeHistoryService : IDownloadHistoryService
    {
        public Task<IReadOnlyList<DownloadHistoryEntry>> LoadAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DownloadHistoryEntry>>([]);
        public Task AppendAsync(DownloadHistoryEntry entry, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task ClearAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeEventLogService : IDownloadEventLogService
    {
#pragma warning disable CS0067 // event required by interface but not exercised in tests
        public event EventHandler<DownloadEventRecord>? EventRecorded;
#pragma warning restore CS0067
        public void Log(DownloaderLogLevel level, string message, Guid? jobId = null) { }
        public Task<string> ExportDiagnosticsAsync(CancellationToken ct = default)
            => Task.FromResult(Path.GetTempFileName());
    }
}
