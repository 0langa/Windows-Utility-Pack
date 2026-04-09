using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>
/// Downloads and manages external tool binaries (yt-dlp, gallery-dl, ffmpeg)
/// in the local application tools directory.
/// </summary>
public class DependencyManagerService : IDependencyManagerService
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "WindowsUtilityPack/1.0" }
        }
    };

    private static readonly string ToolDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUtilityPack", "tools");

    /// <inheritdoc/>
    public string YtDlpPath => Path.Combine(ToolDir, "yt-dlp.exe");

    /// <inheritdoc/>
    public string GalleryDlPath => Path.Combine(ToolDir, "gallery-dl.exe");

    /// <inheritdoc/>
    public string FfmpegPath => Path.Combine(ToolDir, "ffmpeg.exe");

    /// <inheritdoc/>
    public DependencyStatus Check()
    {
        return new DependencyStatus(
            File.Exists(YtDlpPath),
            File.Exists(GalleryDlPath),
            File.Exists(FfmpegPath));
    }

    /// <inheritdoc/>
    public async Task EnsureAllAsync(Action<string> onProgress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ToolDir);

        if (!File.Exists(YtDlpPath))
        {
            onProgress("Downloading yt-dlp...");
            await DownloadYtDlpAsync(ct);
            onProgress("yt-dlp installed.");
        }

        if (!File.Exists(GalleryDlPath))
        {
            onProgress("Downloading gallery-dl...");
            await DownloadGalleryDlAsync(ct);
            onProgress("gallery-dl installed.");
        }

        if (!File.Exists(FfmpegPath))
        {
            onProgress("Downloading ffmpeg...");
            await DownloadFfmpegAsync(ct);
            onProgress("ffmpeg installed.");
        }

        onProgress("All tools ready.");
    }

    /// <inheritdoc/>
    public async Task<string> UpdateYtDlpAsync(CancellationToken ct = default)
    {
        if (!File.Exists(YtDlpPath))
        {
            return "yt-dlp is not installed.";
        }

        var psi = new ProcessStartInfo
        {
            FileName = YtDlpPath,
            Arguments = "-U",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return "Failed to start yt-dlp update process.";
        }

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return output.Trim();
    }

    private async Task DownloadYtDlpAsync(CancellationToken ct)
    {
        var json = await Http.GetStringAsync(
            "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest", ct);

        var release = JObject.Parse(json);
        var assets = release["assets"] as JArray
            ?? throw new InvalidOperationException("No assets found in yt-dlp release.");

        string? downloadUrl = null;
        foreach (var asset in assets)
        {
            var name = asset["name"]?.ToString() ?? string.Empty;
            if (name.Equals("yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset["browser_download_url"]?.ToString();
                break;
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException("yt-dlp.exe asset not found in latest release.");
        }

        await DownloadToFileAsync(downloadUrl, YtDlpPath, ct);
    }

    private async Task DownloadGalleryDlAsync(CancellationToken ct)
    {
        var json = await Http.GetStringAsync(
            "https://api.github.com/repos/mikf/gallery-dl/releases/latest", ct);

        var release = JObject.Parse(json);
        var assets = release["assets"] as JArray
            ?? throw new InvalidOperationException("No assets found in gallery-dl release.");

        string? downloadUrl = null;
        foreach (var asset in assets)
        {
            var name = asset["name"]?.ToString() ?? string.Empty;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("zip", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset["browser_download_url"]?.ToString();
                break;
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException("gallery-dl .exe asset not found in latest release.");
        }

        await DownloadToFileAsync(downloadUrl, GalleryDlPath, ct);
    }

    private async Task DownloadFfmpegAsync(CancellationToken ct)
    {
        // Fix Issue 21: use the GitHub Releases API instead of a hardcoded artifact URL
        // so the download stays valid across renamed build artefacts.
        var json = await Http.GetStringAsync(
            "https://api.github.com/repos/yt-dlp/FFmpeg-Builds/releases/latest", ct);

        var release = JObject.Parse(json);
        var assets = release["assets"] as JArray
            ?? throw new InvalidOperationException("No assets found in FFmpeg-Builds release.");

        string? downloadUrl = null;
        foreach (var asset in assets)
        {
            var name = asset["name"]?.ToString() ?? string.Empty;
            if (name.Contains("win64-gpl", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset["browser_download_url"]?.ToString();
                break;
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException("win64-gpl.zip asset not found in FFmpeg-Builds release.");
        }

        var tmpZip = Path.Combine(ToolDir, "ffmpeg.zip.tmp");

        try
        {
            await DownloadToFileAsync(downloadUrl, tmpZip, ct);

            using var archive = ZipFile.OpenRead(tmpZip);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.Contains("bin/", StringComparison.OrdinalIgnoreCase)
                    && entry.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(FfmpegPath, overwrite: true);
                    break;
                }
            }
        }
        finally
        {
            if (File.Exists(tmpZip))
            {
                try { File.Delete(tmpZip); }
                catch (IOException) { }
            }
        }
    }

    private static async Task DownloadToFileAsync(string url, string destPath, CancellationToken ct)
    {
        var tmpPath = destPath + ".tmp";
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await stream.CopyToAsync(fs, ct);
        }
        catch
        {
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); }
                catch (IOException) { }
            }
            throw;
        }

        File.Move(tmpPath, destPath, overwrite: true);
    }
}
