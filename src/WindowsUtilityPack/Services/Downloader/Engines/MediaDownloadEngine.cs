using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader.Engines;

/// <summary>Media engine backed by yt-dlp for video/audio extraction workflows.</summary>
public sealed partial class MediaDownloadEngine : DownloadEngineBase
{
    private readonly IDependencyManagerService _dependencyManager;

    public MediaDownloadEngine(IDependencyManagerService dependencyManager)
    {
        _dependencyManager = dependencyManager;
    }

    public override DownloadEngineType EngineType => DownloadEngineType.Media;

    public override bool CanHandle(DownloadJob job, DownloaderSettings settings)
    {
        if (job.Mode == DownloaderMode.MediaDownload)
        {
            return true;
        }

        if (!Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Fix Issue 16: delegate to the centralised host list so engine and ViewModel stay in sync
        return DownloaderKnownHosts.Matches(uri.Host, DownloaderKnownHosts.MediaHosts);
    }

    public override Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
    {
        var uri = Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var parsed)
            ? parsed
            : null;

        var title = uri is null ? "Media item" : $"{uri.Host} media";

        return Task.FromResult(new DownloadProbeResult
        {
            DisplayTitle = title,
            ResolvedUrl = job.SourceUrl,
            SuggestedFileName = "media-download.mp4",
            SupportsResume = true,
            SuggestedSegments = 1,
            SelectedProfile = settings.Media.PreferredVideoFormat,
        });
    }

    public override async Task ExecuteAsync(
        DownloadJob job,
        DownloaderSettings settings,
        DownloadExecutionPlan plan,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_dependencyManager.YtDlpPath))
        {
            throw new InvalidOperationException("yt-dlp is not installed. Install downloader tools first.");
        }

        Directory.CreateDirectory(plan.TargetDirectory);

        var profile = plan.MediaProfile;
        var outputTemplate = string.IsNullOrWhiteSpace(settings.Media.OutputTemplate)
            ? "%(title)s.%(ext)s"
            : settings.Media.OutputTemplate;

        var args = new List<string>
        {
            "--newline",
            "--progress",
            "--restrict-filenames",
            "--paths",
            QuoteArg(plan.TargetDirectory),
            "-o",
            QuoteArg(outputTemplate),
            "-f",
            QuoteArg(string.IsNullOrWhiteSpace(profile.FormatExpression) ? settings.Media.PreferredVideoFormat : profile.FormatExpression),
        };

        if (!profile.AllowPlaylist && !settings.Media.AllowPlaylist)
        {
            args.Add("--no-playlist");
        }

        if (profile.DownloadSubtitles || settings.Media.DownloadSubtitles)
        {
            args.Add("--write-subs");
            args.Add("--write-auto-subs");
        }

        if (profile.DownloadThumbnail || settings.Media.DownloadThumbnail)
        {
            args.Add("--write-thumbnail");
        }

        if (profile.EmbedMetadata || settings.Media.EmbedMetadata)
        {
            args.Add("--embed-metadata");
        }

        if (profile.AudioOnly)
        {
            args.Add("--extract-audio");
            args.Add("--audio-format");
            args.Add(QuoteArg(string.IsNullOrWhiteSpace(profile.PreferredAudioFormat)
                ? settings.Media.PreferredAudioFormat
                : profile.PreferredAudioFormat));
        }
        else if (!string.IsNullOrWhiteSpace(profile.PreferredContainer))
        {
            args.Add("--merge-output-format");
            args.Add(QuoteArg(profile.PreferredContainer));
        }

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
            FileName = _dependencyManager.YtDlpPath,
            Arguments = string.Join(" ", args),
            WorkingDirectory = plan.TargetDirectory,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start yt-dlp process.");

        string lastError = string.Empty;
        string detectedFile = string.Empty;
        var stopwatch = Stopwatch.StartNew();

        async Task ReadLinesAsync(StreamReader reader)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (line.Contains("Destination:", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = line.IndexOf(':');
                    if (idx > -1)
                    {
                        detectedFile = line[(idx + 1)..].Trim();
                    }
                }

                var progressMatch = YtdlpProgressRegex().Match(line);
                if (progressMatch.Success)
                {
                    var pct = TryParseDouble(progressMatch.Groups[1].Value);
                    var speed = ParseSpeed(progressMatch.Groups[2].Value);

                    progress.Report(new DownloadProgressUpdate
                    {
                        Status = DownloadJobStatus.Downloading,
                        ProgressPercent = pct,
                        SpeedBytesPerSecond = speed,
                        StatusMessage = "Media download",
                    });
                }
                else if (line.Contains("[Merger]", StringComparison.OrdinalIgnoreCase)
                         || line.Contains("[ffmpeg]", StringComparison.OrdinalIgnoreCase))
                {
                    progress.Report(new DownloadProgressUpdate
                    {
                        Status = DownloadJobStatus.Processing,
                        StatusMessage = "Post-processing media",
                    });
                }

                if (!string.IsNullOrWhiteSpace(line) && line.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    lastError = line;
                }
            }
        }

        var stdoutTask = ReadLinesAsync(process.StandardOutput);
        var stderrTask = ReadLinesAsync(process.StandardError);

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(lastError)
                ? "yt-dlp exited with a non-zero code."
                : lastError);
        }

        var outputPath = string.IsNullOrWhiteSpace(detectedFile)
            ? string.Empty
            : Path.Combine(plan.TargetDirectory, detectedFile);

        progress.Report(new DownloadProgressUpdate
        {
            Status = DownloadJobStatus.Completed,
            StatusMessage = $"Media download complete in {stopwatch.Elapsed:mm\\:ss}.",
            ProgressPercent = 100,
            OutputFilePath = outputPath,
        });
    }

    private static double TryParseDouble(string value)
    {
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static double ParseSpeed(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        // Example: 3.11MiB/s
        var match = SpeedRegex().Match(value);
        if (!match.Success)
        {
            return 0;
        }

        var amount = TryParseDouble(match.Groups[1].Value);
        var unit = match.Groups[2].Value.ToUpperInvariant();

        return unit switch
        {
            "KIB" => amount * 1024,
            "MIB" => amount * 1024 * 1024,
            "GIB" => amount * 1024 * 1024 * 1024,
            "KB" => amount * 1000,
            "MB" => amount * 1000 * 1000,
            "GB" => amount * 1000 * 1000 * 1000,
            _ => amount,
        };
    }

    [GeneratedRegex(@"\[download\]\s+([\d.]+)%.*?at\s+([^\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex YtdlpProgressRegex();

    [GeneratedRegex(@"([\d.]+)\s*([KMG]?i?B)", RegexOptions.IgnoreCase)]
    private static partial Regex SpeedRegex();
}
