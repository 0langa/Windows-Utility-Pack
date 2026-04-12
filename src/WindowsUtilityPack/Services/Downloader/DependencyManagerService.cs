using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>
/// Downloads and manages external tool binaries (yt-dlp, gallery-dl, ffmpeg)
/// in the local application tools directory.
/// </summary>
public class DependencyManagerService : IDependencyManagerService
{
    private static readonly Regex ChecksumLineRegex = new(@"^(?<hash>[A-Fa-f0-9]{64})\s+[* ](?<file>.+)$", RegexOptions.Compiled);
    private static readonly Regex OpenSslStyleRegex = new(@"^SHA256\s*\((?<file>.+)\)\s*=\s*(?<hash>[A-Fa-f0-9]{64})$", RegexOptions.Compiled);

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
        string? checksumsUrl = null;
        foreach (var asset in assets)
        {
            var name = asset["name"]?.ToString() ?? string.Empty;
            if (name.Equals("yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset["browser_download_url"]?.ToString();
                continue;
            }

            if (name.Contains("SHA2-256SUMS", StringComparison.OrdinalIgnoreCase))
            {
                checksumsUrl = asset["browser_download_url"]?.ToString();
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException("yt-dlp.exe asset not found in latest release.");
        }

        if (string.IsNullOrWhiteSpace(checksumsUrl))
        {
            throw new InvalidOperationException("yt-dlp checksum manifest not found in latest release.");
        }

        var expectedHash = await ResolveExpectedSha256Async(checksumsUrl, "yt-dlp.exe", ct);
        await DownloadToFileAsync(downloadUrl, YtDlpPath, ct, expectedHash);
    }

    private async Task DownloadGalleryDlAsync(CancellationToken ct)
    {
        var json = await Http.GetStringAsync(
            "https://api.github.com/repos/mikf/gallery-dl/releases/latest", ct);

        var release = JObject.Parse(json);
        var assets = release["assets"] as JArray
            ?? throw new InvalidOperationException("No assets found in gallery-dl release.");

        string? downloadUrl = null;
        string? checksumsUrl = null;
        string? selectedAssetName = null;
        foreach (var asset in assets)
        {
            var name = asset["name"]?.ToString() ?? string.Empty;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("zip", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset["browser_download_url"]?.ToString();
                selectedAssetName = name;
                continue;
            }

            if (name.Contains("checksum", StringComparison.OrdinalIgnoreCase)
                || name.Contains("sha256", StringComparison.OrdinalIgnoreCase)
                || name.Contains("sha-256", StringComparison.OrdinalIgnoreCase))
            {
                checksumsUrl = asset["browser_download_url"]?.ToString();
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException("gallery-dl .exe asset not found in latest release.");
        }

        if (string.IsNullOrWhiteSpace(checksumsUrl) || string.IsNullOrWhiteSpace(selectedAssetName))
        {
            throw new InvalidOperationException("gallery-dl checksum manifest not found in latest release.");
        }

        var expectedHash = await ResolveExpectedSha256Async(checksumsUrl, selectedAssetName, ct);
        await DownloadToFileAsync(downloadUrl, GalleryDlPath, ct, expectedHash);
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
        string? checksumsUrl = null;
        string? zipAssetName = null;
        foreach (var asset in assets)
        {
            var name = asset["name"]?.ToString() ?? string.Empty;
            if (name.Contains("win64-gpl", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset["browser_download_url"]?.ToString();
                zipAssetName = name;
                continue;
            }

            if (name.Contains("checksum", StringComparison.OrdinalIgnoreCase)
                || name.Contains("sha256", StringComparison.OrdinalIgnoreCase)
                || name.Contains("sha-256", StringComparison.OrdinalIgnoreCase))
            {
                checksumsUrl = asset["browser_download_url"]?.ToString();
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException("win64-gpl.zip asset not found in FFmpeg-Builds release.");
        }

        if (string.IsNullOrWhiteSpace(checksumsUrl) || string.IsNullOrWhiteSpace(zipAssetName))
        {
            throw new InvalidOperationException("FFmpeg checksum manifest not found in latest release.");
        }

        var tmpZip = Path.Combine(ToolDir, "ffmpeg.zip.tmp");

        try
        {
            var expectedHash = await ResolveExpectedSha256Async(checksumsUrl, zipAssetName, ct);
            await DownloadToFileAsync(downloadUrl, tmpZip, ct, expectedHash);

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

    private static async Task DownloadToFileAsync(string url, string destPath, CancellationToken ct, string? expectedSha256 = null)
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

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            await VerifySha256Async(tmpPath, expectedSha256, ct);
        }

        File.Move(tmpPath, destPath, overwrite: true);
    }

    private static async Task<string> ResolveExpectedSha256Async(string checksumManifestUrl, string assetFileName, CancellationToken ct)
    {
        using var response = await Http.GetAsync(checksumManifestUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var manifest = await response.Content.ReadAsStringAsync(ct);
        var expected = TryExtractSha256(manifest, assetFileName);
        if (string.IsNullOrWhiteSpace(expected))
        {
            throw new InvalidOperationException($"SHA-256 for '{assetFileName}' not found in release checksum manifest.");
        }

        return expected;
    }

    private static string? TryExtractSha256(string manifestText, string assetFileName)
    {
        foreach (var rawLine in manifestText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var checksumMatch = ChecksumLineRegex.Match(line);
            if (checksumMatch.Success && FileNameMatches(checksumMatch.Groups["file"].Value, assetFileName))
            {
                return checksumMatch.Groups["hash"].Value.ToLowerInvariant();
            }

            var openSslMatch = OpenSslStyleRegex.Match(line);
            if (openSslMatch.Success && FileNameMatches(openSslMatch.Groups["file"].Value, assetFileName))
            {
                return openSslMatch.Groups["hash"].Value.ToLowerInvariant();
            }
        }

        return null;
    }

    internal static string? ExtractSha256FromManifest(string manifestText, string assetFileName)
        => TryExtractSha256(manifestText, assetFileName);

    private static bool FileNameMatches(string candidate, string expectedFileName)
        => string.Equals(Path.GetFileName(candidate.Trim().Trim('"')), expectedFileName, StringComparison.OrdinalIgnoreCase);

    private static async Task VerifySha256Async(string filePath, string expectedSha256, CancellationToken ct)
    {
        await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var digest = await SHA256.HashDataAsync(file, ct);
        var actual = Convert.ToHexString(digest).ToLowerInvariant();
        var normalizedExpected = expectedSha256.Trim().ToLowerInvariant();

        if (!string.Equals(actual, normalizedExpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"SHA-256 mismatch for downloaded dependency. Expected {normalizedExpected}, got {actual}.");
        }
    }
}
