using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Chooses the most suitable engine and probe metadata for each queued job.</summary>
public sealed class DownloadEngineResolverService : IDownloadEngineResolver
{
    private readonly IReadOnlyList<IDownloadEngine> _engines;

    public DownloadEngineResolverService(IEnumerable<IDownloadEngine> engines)
    {
        _engines = engines.ToList();
    }

    public async Task<DownloadResolutionResult> ResolveAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
    {
        var ordered = _engines
            .Where(engine => engine.CanHandle(job, settings))
            .OrderByDescending(engine => Score(job, engine.EngineType))
            .ToList();

        if (ordered.Count == 0)
        {
            throw new InvalidOperationException("No download engine can handle this URL.");
        }

        var warnings = new List<string>();

        foreach (var engine in ordered)
        {
            try
            {
                var probe = await engine.ProbeAsync(job, settings, cancellationToken);
                return new DownloadResolutionResult
                {
                    Engine = engine,
                    Probe = probe,
                    Warnings = warnings,
                };
            }
            catch (Exception ex)
            {
                warnings.Add($"{engine.EngineType} probe failed: {ex.Message}");
            }
        }

        // If all probes failed, keep first engine to allow execution attempt with fallback metadata.
        return new DownloadResolutionResult
        {
            Engine = ordered[0],
            Probe = new DownloadProbeResult
            {
                DisplayTitle = job.SourceUrl,
                ResolvedUrl = job.SourceUrl,
                SuggestedFileName = "download.bin",
                SuggestedSegments = 1,
                SupportsResume = false,
            },
            Warnings = warnings,
        };
    }

    private static int Score(DownloadJob job, DownloadEngineType engineType)
    {
        return (job.Mode, engineType) switch
        {
            (DownloaderMode.MediaDownload, DownloadEngineType.Media) => 100,
            (DownloaderMode.AssetGrabber, DownloadEngineType.Gallery) => 95,
            (DownloaderMode.SiteCrawl, DownloadEngineType.Gallery) => 90,
            (DownloaderMode.QuickDownload, DownloadEngineType.DirectHttp) => 80,
            (_, DownloadEngineType.Fallback) => 10,
            _ => 50,
        };
    }
}
