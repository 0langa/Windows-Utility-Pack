using System.Diagnostics;
using System.IO;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader.Engines;

/// <summary>gallery-dl based engine for gallery/collection style downloads.</summary>
public sealed class GalleryDownloadEngine : DownloadEngineBase
{
    private readonly IDependencyManagerService _dependencyManager;

    public GalleryDownloadEngine(IDependencyManagerService dependencyManager)
    {
        _dependencyManager = dependencyManager;
    }

    public override DownloadEngineType EngineType => DownloadEngineType.Gallery;

    public override bool CanHandle(DownloadJob job, DownloaderSettings settings)
    {
        if (!Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        return host.Contains("imgur", StringComparison.OrdinalIgnoreCase)
               || host.Contains("reddit", StringComparison.OrdinalIgnoreCase)
               || host.Contains("flickr", StringComparison.OrdinalIgnoreCase)
               || host.Contains("deviantart", StringComparison.OrdinalIgnoreCase)
               || host.Contains("pixiv", StringComparison.OrdinalIgnoreCase)
               || host.Contains("tumblr", StringComparison.OrdinalIgnoreCase);
    }

    public override Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
    {
        var host = Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : "gallery";

        return Task.FromResult(new DownloadProbeResult
        {
            DisplayTitle = $"{host} collection",
            ResolvedUrl = job.SourceUrl,
            SupportsResume = true,
            SuggestedFileName = "gallery-item",
            SuggestedSegments = 1,
        });
    }

    public override async Task ExecuteAsync(
        DownloadJob job,
        DownloaderSettings settings,
        DownloadExecutionPlan plan,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_dependencyManager.GalleryDlPath))
        {
            throw new InvalidOperationException("gallery-dl is not installed. Install downloader tools first.");
        }

        Directory.CreateDirectory(plan.TargetDirectory);

        var args = new List<string>
        {
            "--dest",
            QuoteArg(plan.TargetDirectory),
            "--write-log",
            QuoteArg(Path.Combine(plan.TargetDirectory, "gallery-dl.log")),
        };

        if (!string.IsNullOrWhiteSpace(settings.Connections.CookieFilePath) && File.Exists(settings.Connections.CookieFilePath))
        {
            args.Add("--cookies");
            args.Add(QuoteArg(settings.Connections.CookieFilePath));
        }

        if (!string.IsNullOrWhiteSpace(settings.Advanced.CustomEngineArguments))
        {
            args.Add(settings.Advanced.CustomEngineArguments);
        }

        args.Add(QuoteArg(job.SourceUrl));

        var psi = new ProcessStartInfo
        {
            FileName = _dependencyManager.GalleryDlPath,
            Arguments = string.Join(" ", args),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = plan.TargetDirectory,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start gallery-dl process.");

        var downloadedFiles = 0;
        var lastError = string.Empty;

        async Task ReadAsync(StreamReader reader)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    downloadedFiles++;
                    progress.Report(new DownloadProgressUpdate
                    {
                        Status = DownloadJobStatus.Downloading,
                        StatusMessage = $"Gallery files downloaded: {downloadedFiles}",
                    });
                }

                if (line.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    lastError = line;
                }
            }
        }

        await Task.WhenAll(ReadAsync(process.StandardOutput), ReadAsync(process.StandardError));
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(lastError)
                ? "gallery-dl failed to download the collection."
                : lastError);
        }

        progress.Report(new DownloadProgressUpdate
        {
            Status = DownloadJobStatus.Completed,
            StatusMessage = $"Gallery complete ({downloadedFiles} file(s)).",
            ProgressPercent = 100,
        });
    }

    private static string QuoteArg(string arg)
    {
        return $"\"{arg.Replace("\"", "\\\"")}\"";
    }
}
