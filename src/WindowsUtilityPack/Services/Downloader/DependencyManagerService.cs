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

    private static readonly SemaphoreSlim InstallationGate = new(1, 1);
    private const long MaximumToolBytes = 512L * 1024 * 1024;

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "WindowsUtilityPack/1.0" }
        }
    };

    private static readonly string ToolDir = Path.Combine(
        AppDataPaths.LocalDirectory, "tools");

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
        await InstallationGate.WaitAsync(ct);
        try { await EnsureAllCoreAsync(onProgress, ct); }
        finally { InstallationGate.Release(); }
    }

    private async Task EnsureAllCoreAsync(Action<string> onProgress, CancellationToken ct)
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
        await InstallationGate.WaitAsync(ct);
        try { return await UpdateYtDlpCoreAsync(ct); }
        finally { InstallationGate.Release(); }
    }

    private async Task<string> UpdateYtDlpCoreAsync(CancellationToken ct)
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

        var result = await new ProcessRunner(TimeSpan.FromMinutes(5)).RunAsync(psi, ct);
        if (result.ExitCode != 0) throw new IOException($"yt-dlp update exited with code {result.ExitCode}: {result.StdErr.Trim()}");
        return result.StdOut.Trim();
    }

    private async Task DownloadYtDlpAsync(CancellationToken ct)
    {
        var json = await ReadMetadataAsync(
            "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest", ct);

        var release = JObject.Parse(json);
        var assets = release["assets"] as JArray
            ?? throw new InvalidOperationException("No assets found in yt-dlp release.");

        string? downloadUrl = null;
        string? checksumsUrl = SelectChecksumManifestUrl(assets, "SHA2-256SUMS");
        foreach (var asset in assets)
        {
            var name = asset["name"]?.ToString() ?? string.Empty;
            if (name.Equals("yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset["browser_download_url"]?.ToString();
                continue;
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
        var json = await ReadMetadataAsync(
            "https://codeberg.org/api/v1/repos/mikf/gallery-dl/releases/latest", ct);

        var release = JObject.Parse(json);
        var assets = release["assets"] as JArray
            ?? throw new InvalidOperationException("No assets found in gallery-dl release.");

        string? downloadUrl = null;
        string? checksumsUrl = SelectChecksumManifestUrl(assets, "SHA256SUMS");
        string? selectedAssetName = null;
        foreach (var asset in assets)
        {
            var name = asset["name"]?.ToString() ?? string.Empty;
            if (name.Equals("gallery-dl.exe", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset["browser_download_url"]?.ToString();
                selectedAssetName = name;
                continue;
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

    internal static string? SelectChecksumManifestUrl(JArray assets, string manifestName) =>
        assets.SingleOrDefault(asset => (asset["name"]?.ToString() ?? string.Empty)
            .Equals(manifestName, StringComparison.OrdinalIgnoreCase))?["browser_download_url"]?.ToString();

    private async Task DownloadFfmpegAsync(CancellationToken ct)
    {
        // Fix Issue 21: use the GitHub Releases API instead of a hardcoded artifact URL
        // so the download stays valid across renamed build artefacts.
        var json = await ReadMetadataAsync(
            "https://api.github.com/repos/yt-dlp/FFmpeg-Builds/releases/latest", ct);

        var release = JObject.Parse(json);
        var assets = release["assets"] as JArray
            ?? throw new InvalidOperationException("No assets found in FFmpeg-Builds release.");

        string? downloadUrl = null;
        string? checksumsUrl = SelectChecksumManifestUrl(assets, "checksums.sha256");
        string? zipAssetName = null;
        foreach (var asset in assets)
        {
            var name = asset["name"]?.ToString() ?? string.Empty;
            if (name.Contains("win64-gpl", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("shared", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset["browser_download_url"]?.ToString();
                zipAssetName = name;
                continue;
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

        var tmpZip = Path.Combine(ToolDir, "ffmpeg-" + Guid.NewGuid().ToString("N") + ".zip.tmp");

        try
        {
            var expectedHash = await ResolveExpectedSha256Async(checksumsUrl, zipAssetName, ct);
            await DownloadToFileAsync(downloadUrl, tmpZip, ct, expectedHash);

            ExtractFfmpeg(tmpZip, FfmpegPath, ct);
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

    internal static void ExtractFfmpeg(string archivePath, string destination, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var archive = ZipFile.OpenRead(archivePath);
        var matches = archive.Entries.Where(e => e.FullName.Replace('\\', '/').Contains("bin/", StringComparison.OrdinalIgnoreCase)
            && e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1 || matches[0].Length <= 0 || matches[0].Length > MaximumToolBytes)
            throw new InvalidDataException("The FFmpeg archive must contain one non-empty bin/ffmpeg.exe (at most 512 MiB).");
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var source = matches[0].Open())
            using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920]; long copied = 0; int count;
                while ((count = source.Read(buffer)) != 0)
                {
                    ct.ThrowIfCancellationRequested();
                    if ((copied += count) > MaximumToolBytes) throw new InvalidDataException("Extracted tool exceeds 512 MiB.");
                    target.Write(buffer, 0, count);
                }
                target.Flush(true);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private static async Task DownloadToFileAsync(string url, string destPath, CancellationToken ct, string? expectedSha256 = null)
    {
        var tmpPath = destPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        var token = timeout.Token;
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumToolBytes) throw new InvalidDataException("Tool download exceeds 512 MiB.");
            await using (var stream = await response.Content.ReadAsStreamAsync(token))
            await using (var fs = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920]; long copied = 0; int count;
                while ((count = await stream.ReadAsync(buffer, token)) != 0)
                {
                    if ((copied += count) > MaximumToolBytes) throw new InvalidDataException("Tool download exceeds 512 MiB.");
                    await fs.WriteAsync(buffer.AsMemory(0, count), token);
                }
                if (copied == 0 || (response.Content.Headers.ContentLength is long expected && copied != expected))
                    throw new InvalidDataException("The tool download is empty or incomplete.");
                await fs.FlushAsync(token);
            }
            if (!string.IsNullOrWhiteSpace(expectedSha256)) await VerifySha256Async(tmpPath, expectedSha256, token);
            token.ThrowIfCancellationRequested();
            File.Move(tmpPath, destPath, overwrite: true);
        }
        finally { File.Delete(tmpPath); }
    }

    private static async Task<string> ReadMetadataAsync(string url, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        return await BoundedHttpContent.ReadTextAsync(response.Content, timeout.Token);
    }

    private static async Task<string> ResolveExpectedSha256Async(string checksumManifestUrl, string assetFileName, CancellationToken ct)
    {
        var manifest = await ReadMetadataAsync(checksumManifestUrl, ct);
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
