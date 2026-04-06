using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>
/// Orchestrates downloads by detecting the best engine (yt-dlp, gallery-dl, or scraper)
/// for each URL and driving the appropriate download process.
/// </summary>
public partial class DownloadEngineService : IDownloadEngineService
{
    private readonly IDependencyManagerService _depManager;
    private readonly IWebScraperService _scraper;

    /// <summary>Initialises a new <see cref="DownloadEngineService"/>.</summary>
    public DownloadEngineService(IDependencyManagerService depManager, IWebScraperService scraper)
    {
        _depManager = depManager;
        _scraper = scraper;
    }

    /// <inheritdoc/>
    public IReadOnlyList<(string Label, string Format)> VideoFormats { get; } =
    [
        ("Best (auto)", "bestvideo+bestaudio/best"),
        ("1080p MP4", "bestvideo[height<=1080][ext=mp4]+bestaudio[ext=m4a]/best[height<=1080]"),
        ("720p MP4", "bestvideo[height<=720][ext=mp4]+bestaudio[ext=m4a]/best[height<=720]"),
        ("480p MP4", "bestvideo[height<=480][ext=mp4]+bestaudio[ext=m4a]/best[height<=480]"),
        ("Audio only (best)", "bestaudio/best"),
        ("Audio only (MP3)", "bestaudio/best"),
        ("Audio only (FLAC)", "bestaudio/best"),
    ];

    /// <inheritdoc/>
    public async Task DetectEngineAsync(DownloadItem item, CancellationToken ct = default)
    {
        item.Status = "Detecting";

        // Try yt-dlp
        if (File.Exists(_depManager.YtDlpPath))
        {
            var exitCode = await RunProcessAsync(
                _depManager.YtDlpPath,
                $"--simulate --no-playlist \"{item.Url}\"",
                ct);

            if (exitCode == 0)
            {
                item.Engine = "yt-dlp";

                var title = await GetProcessOutputAsync(
                    _depManager.YtDlpPath,
                    $"--get-title --no-playlist \"{item.Url}\"",
                    ct);

                var firstLine = title
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault()?.Trim();

                item.Title = !string.IsNullOrEmpty(firstLine) ? firstLine : GetHostName(item.Url);
                item.Status = "Queued";
                return;
            }
        }

        // Try gallery-dl
        if (File.Exists(_depManager.GalleryDlPath))
        {
            var exitCode = await RunProcessAsync(
                _depManager.GalleryDlPath,
                $"--list-urls \"{item.Url}\"",
                ct);

            if (exitCode == 0)
            {
                item.Engine = "gallery-dl";
                item.Title = GetHostName(item.Url);
                item.Status = "Queued";
                return;
            }
        }

        // Fallback to scraper
        item.Engine = "Scraper";
        item.Title = GetHostName(item.Url);
        item.Status = "Queued";
    }

    /// <inheritdoc/>
    public async Task<List<ScrapedAsset>?> DownloadAsync(DownloadItem item, CancellationToken ct = default)
    {
        if (item.Engine == "yt-dlp")
        {
            await DownloadWithYtDlpAsync(item, ct);
            return null;
        }

        if (item.Engine == "gallery-dl")
        {
            await DownloadWithGalleryDlAsync(item, ct);
            return null;
        }

        // Scraper path — return assets for user selection
        item.Status = "Scraping";
        var assets = await _scraper.ScrapeAsync(item.Url, false, 1, 1, null, ct);
        item.Status = "Queued";
        return assets;
    }

    /// <inheritdoc/>
    public async Task DownloadScrapedAssetsAsync(
        IEnumerable<ScrapedAsset> assets,
        string outputDir,
        bool crawlSubdirectories,
        int maxDepth,
        int maxPages,
        Action<double> onProgress,
        CancellationToken ct = default)
    {
        var assetList = assets.ToList();
        if (assetList.Count == 0)
        {
            return;
        }

        int completed = 0;

        foreach (var asset in assetList)
        {
            ct.ThrowIfCancellationRequested();
            await _scraper.DownloadAssetAsync(asset, outputDir, null, ct);
            completed++;
            onProgress((double)completed / assetList.Count * 100);
        }
    }

    private async Task DownloadWithYtDlpAsync(DownloadItem item, CancellationToken ct)
    {
        item.Status = "Downloading";

        var formatEntry = VideoFormats.FirstOrDefault(f => f.Label == item.SelectedFormat);
        var formatStr = formatEntry.Format ?? "bestvideo+bestaudio/best";

        var args = $"-f \"{formatStr}\" --ffmpeg-location \"{Path.GetDirectoryName(_depManager.FfmpegPath)}\" --no-playlist --no-warnings --progress -o \"%(title)s.%(ext)s\"";

        // Audio extraction for MP3/FLAC
        if (item.SelectedFormat == "Audio only (MP3)")
        {
            args += " --extract-audio --audio-format mp3";
        }
        else if (item.SelectedFormat == "Audio only (FLAC)")
        {
            args += " --extract-audio --audio-format flac";
        }

        args += $" \"{item.Url}\"";

        var psi = new ProcessStartInfo
        {
            FileName = _depManager.YtDlpPath,
            Arguments = args,
            WorkingDirectory = item.SavePath,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            item.Status = "Failed";
            item.Speed = "Failed to start yt-dlp";
            return;
        }

        string lastStderrLine = string.Empty;

        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync(ct)) is not null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lastStderrLine = line;
                }
            }
        }, ct);

        string? stdoutLine;
        while ((stdoutLine = await process.StandardOutput.ReadLineAsync(ct)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(stdoutLine))
            {
                continue;
            }

            if (stdoutLine.Contains("[download]"))
            {
                var progressMatch = YtDlpProgressRegex().Match(stdoutLine);
                if (progressMatch.Success)
                {
                    if (double.TryParse(progressMatch.Groups[1].Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double pct))
                    {
                        item.Progress = pct;
                    }

                    if (progressMatch.Groups[2].Success)
                    {
                        item.TotalSize = progressMatch.Groups[2].Value;
                    }

                    if (progressMatch.Groups[3].Success)
                    {
                        item.Speed = progressMatch.Groups[3].Value;
                    }

                    if (progressMatch.Groups[4].Success)
                    {
                        item.Eta = progressMatch.Groups[4].Value;
                    }
                }

                if (stdoutLine.Contains("100%"))
                {
                    item.Progress = 100;
                }
            }
            else if (stdoutLine.Contains("[Merger]") || stdoutLine.Contains("[ffmpeg]"))
            {
                item.Status = "Processing";
            }
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            item.Speed = lastStderrLine;
            item.Status = "Failed";
        }
        else
        {
            item.Progress = 100;
            item.Speed = string.Empty;
            item.Status = "Complete";
        }
    }

    private async Task DownloadWithGalleryDlAsync(DownloadItem item, CancellationToken ct)
    {
        item.Status = "Downloading";

        var psi = new ProcessStartInfo
        {
            FileName = _depManager.GalleryDlPath,
            Arguments = $"--dest \"{item.SavePath}\" \"{item.Url}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            item.Status = "Failed";
            item.Speed = "Failed to start gallery-dl";
            return;
        }

        int fileCount = 0;

        string? galleryLine;
        while ((galleryLine = await process.StandardOutput.ReadLineAsync(ct)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(galleryLine) && galleryLine.StartsWith('#'))
            {
                fileCount++;
                item.Speed = $"{fileCount} files";
            }
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            item.Status = "Failed";
        }
        else
        {
            item.Progress = 100;
            item.Speed = $"{fileCount} files downloaded";
            item.Status = "Complete";
        }
    }

    private static async Task<int> RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return -1;
        }

        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }

    private static async Task<string> GetProcessOutputAsync(string fileName, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return string.Empty;
        }

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return output;
    }

    private static string GetHostName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return "Unknown";
    }

    [GeneratedRegex(@"\[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+\s*\w+)?\s*at\s+([\d.]+\s*\w+/s)?\s*ETA\s+(\S+)?")]
    private static partial Regex YtDlpProgressRegex();
}
