using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader.Engines;

/// <summary>Fallback engine that routes unresolved jobs to direct HTTP handling.</summary>
public sealed class FallbackDownloadEngine : DownloadEngineBase
{
    private readonly DirectHttpDownloadEngine _direct;

    public FallbackDownloadEngine(DirectHttpDownloadEngine direct)
    {
        _direct = direct;
    }

    public override DownloadEngineType EngineType => DownloadEngineType.Fallback;

    public override bool CanHandle(DownloadJob job, DownloaderSettings settings)
    {
        return job.Mode is DownloaderMode.QuickDownload or DownloaderMode.MediaDownload;
    }

    public override Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
    {
        if (!_direct.CanHandle(job, settings))
        {
            throw new InvalidOperationException("Fallback direct download is not valid for discovery modes.");
        }

        return _direct.ProbeAsync(job, settings, cancellationToken);
    }

    public override Task ExecuteAsync(
        DownloadJob job,
        DownloaderSettings settings,
        DownloadExecutionPlan plan,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        if (!_direct.CanHandle(job, settings))
        {
            throw new InvalidOperationException("Fallback direct download is not valid for discovery modes.");
        }

        return _direct.ExecuteAsync(job, settings, plan, progress, cancellationToken);
    }
}
