using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader.Engines;

/// <summary>Direct HTTP/HTTPS engine with metadata probe, resume support, and segmented downloads.</summary>
/// <remarks>
/// Fix Issue 5: A single <see cref="HttpClient"/> is reused for the lifetime of the engine instance
/// rather than being created per-request, preventing socket exhaustion under concurrent downloads.
/// </remarks>
public sealed class DirectHttpDownloadEngine : DownloadEngineBase, IDisposable
{
    private const int DefaultBufferSize = 1024 * 64;

    // Fix Issue 5: one long-lived client per engine instance; recreated only when settings change
    private HttpClient? _httpClient;
    private DownloaderSettings? _lastSettings;
    private readonly object _clientLock = new();

    public override DownloadEngineType EngineType => DownloadEngineType.DirectHttp;

    public void Dispose()
    {
        lock (_clientLock)
        {
            _httpClient?.Dispose();
            _httpClient = null;
        }
    }

    public override bool CanHandle(DownloadJob job, DownloaderSettings settings)
    {
        return Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && job.Mode != DownloaderMode.AssetGrabber
               && job.Mode != DownloaderMode.SiteCrawl;
    }

    public override async Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken)
    {
        var result = new DownloadProbeResult
        {
            DisplayTitle = job.SourceUrl,
            ResolvedUrl = job.SourceUrl,
        };

        var client = GetOrCreateClient(settings);
        using var request = new HttpRequestMessage(HttpMethod.Head, job.SourceUrl);
        string? contentType = null;

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            contentType = response.Content.Headers.ContentType?.MediaType;

            if (response.Headers.Location is not null)
            {
                result.ResolvedUrl = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location.ToString()
                    : new Uri(new Uri(job.SourceUrl), response.Headers.Location).ToString();
            }

            result.TotalBytes = response.Content.Headers.ContentLength;
            result.SupportsResume = response.Headers.AcceptRanges.Any(range => range.Equals("bytes", StringComparison.OrdinalIgnoreCase));

            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? Path.GetFileName(new Uri(result.ResolvedUrl).LocalPath);

            fileName = string.IsNullOrWhiteSpace(fileName)
                ? BuildFallbackFileName(result.ResolvedUrl, contentType)
                : fileName.Trim('"');
            result.SuggestedFileName = SanitizeFileName(fileName);

            if (result.TotalBytes is > 0 && result.SupportsResume)
            {
                var thresholdBytes = Math.Max(1, settings.Connections.SegmentThresholdMb) * 1024L * 1024L;
                if (result.TotalBytes >= thresholdBytes)
                {
                    result.SuggestedSegments = Math.Clamp(settings.Connections.SegmentsPerDownload, 1, 8);
                }
            }

            result.DisplayTitle = result.SuggestedFileName;
        }
        catch
        {
            result.SuggestedFileName = SanitizeFileName(BuildFallbackFileName(job.SourceUrl, contentType));
            result.DisplayTitle = result.SuggestedFileName;
            result.SuggestedSegments = 1;
        }

        return result;
    }

    public override async Task ExecuteAsync(
        DownloadJob job,
        DownloaderSettings settings,
        DownloadExecutionPlan plan,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(plan.TargetDirectory);

        var targetFileName = SanitizeFileName(plan.TargetFileName);
        var targetPath = Path.Combine(plan.TargetDirectory, targetFileName);

        if (File.Exists(targetPath))
        {
            if (plan.AutoRenameExisting)
            {
                targetPath = GetUniqueFilePath(plan.TargetDirectory, targetFileName);
            }
            else if (!plan.OverwriteExisting)
            {
                progress.Report(new DownloadProgressUpdate
                {
                    Status = DownloadJobStatus.Skipped,
                    StatusMessage = "Skipped: target file already exists.",
                    ProgressPercent = 100,
                    OutputFilePath = targetPath,
                });
                return;
            }
        }

        var partPath = settings.FileHandling.UsePartFiles
            ? targetPath + ".part"
            : targetPath;

        var segmentCount = Math.Clamp(job.SegmentCount, 1, 8);
        var canSegment = segmentCount > 1
                         && job.TotalBytes is > 0
                         && job.IsResumable
                         && !File.Exists(partPath);

        var client = GetOrCreateClient(settings);

        if (canSegment)
        {
            await DownloadSegmentedAsync(job.SourceUrl, partPath, job.TotalBytes!.Value, segmentCount, client, settings, progress, cancellationToken);
        }
        else
        {
            await DownloadSingleAsync(job.SourceUrl, partPath, client, settings, progress, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(partPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(partPath, targetPath, overwrite: true);
        }

        progress.Report(new DownloadProgressUpdate
        {
            Status = DownloadJobStatus.Completed,
            StatusMessage = "Download complete.",
            ProgressPercent = 100,
            OutputFilePath = targetPath,
        });
    }

    private static async Task DownloadSingleAsync(
        string url,
        string outputPath,
        HttpClient client,
        DownloaderSettings settings,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        long resumeBytes = 0;
        if (settings.FileHandling.ResumePartialFiles && File.Exists(outputPath))
        {
            resumeBytes = new FileInfo(outputPath).Length;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (resumeBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(resumeBytes, null);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (resumeBytes > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            resumeBytes = 0;
        }

        var totalLength = response.Content.Headers.ContentLength;
        var expectedTotal = totalLength.HasValue ? totalLength.Value + resumeBytes : (long?)null;

        var fileMode = resumeBytes > 0 && response.StatusCode == HttpStatusCode.PartialContent
            ? FileMode.Append
            : FileMode.Create;

        if (fileMode == FileMode.Create && File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        await using var output = new FileStream(outputPath, fileMode, FileAccess.Write, FileShare.None, DefaultBufferSize, useAsync: true);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[DefaultBufferSize];
        long downloaded = resumeBytes;

        // Fix Issue 3: track only session bytes for bandwidth throttle math,
        // so a resume offset does not inflate the expected-elapsed calculation.
        long sessionDownloaded = 0;

        var stopwatch = Stopwatch.StartNew();
        var sampleStopwatch = Stopwatch.StartNew();
        long sampleBytes = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

            downloaded += read;
            sessionDownloaded += read;
            sampleBytes += read;

            if (settings.Connections.BandwidthLimitKbps > 0)
            {
                // Fix Issue 3: use sessionDownloaded (not total downloaded) so resumed
                // downloads do not compute a massive initial throttle delay.
                var targetBytesPerSecond = settings.Connections.BandwidthLimitKbps * 1024d / 8d;
                var expectedElapsed = TimeSpan.FromSeconds(sessionDownloaded / targetBytesPerSecond);
                var delay = expectedElapsed - stopwatch.Elapsed;
                if (delay > TimeSpan.FromMilliseconds(25))
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (sampleStopwatch.ElapsedMilliseconds >= 300)
            {
                var seconds = Math.Max(0.001, sampleStopwatch.Elapsed.TotalSeconds);
                var speed = sampleBytes / seconds;

                TimeSpan? eta = null;
                double? percent = null;

                if (expectedTotal is > 0)
                {
                    var remainingBytes = Math.Max(0, expectedTotal.Value - downloaded);
                    eta = speed > 0 ? TimeSpan.FromSeconds(remainingBytes / speed) : null;
                    percent = expectedTotal.Value == 0 ? 0 : downloaded * 100d / expectedTotal.Value;
                }

                progress.Report(new DownloadProgressUpdate
                {
                    Status = DownloadJobStatus.Downloading,
                    DownloadedBytes = downloaded,
                    TotalBytes = expectedTotal,
                    SpeedBytesPerSecond = speed,
                    Eta = eta,
                    ProgressPercent = percent,
                    ActiveSegments = 1,
                    StatusMessage = resumeBytes > 0 ? "Resuming" : "Downloading",
                });

                sampleBytes = 0;
                sampleStopwatch.Restart();
            }
        }

        progress.Report(new DownloadProgressUpdate
        {
            Status = DownloadJobStatus.Processing,
            DownloadedBytes = downloaded,
            TotalBytes = expectedTotal,
            ProgressPercent = 100,
            ActiveSegments = 1,
            StatusMessage = "Finalizing file",
        });
    }

    private static async Task DownloadSegmentedAsync(
        string url,
        string outputPath,
        long totalBytes,
        int segmentCount,
        HttpClient client,
        DownloaderSettings settings,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        var segmentFiles = new List<string>(segmentCount);
        long downloadedTotal = 0;
        var stopwatch = Stopwatch.StartNew();
        var ranges = BuildRanges(totalBytes, segmentCount);

        try
        {
            await Parallel.ForEachAsync(
                ranges,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = segmentCount,
                },
                async (range, ct) =>
                {
                    var segmentFile = outputPath + $".seg{range.index:00}";
                    lock (segmentFiles)
                    {
                        segmentFiles.Add(segmentFile);
                    }

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = new RangeHeaderValue(range.start, range.end);

                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();

                    await using var inStream = await response.Content.ReadAsStreamAsync(ct);
                    await using var outStream = new FileStream(segmentFile, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, useAsync: true);
                    var buffer = new byte[DefaultBufferSize];

                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        var read = await inStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                        if (read <= 0)
                        {
                            break;
                        }

                        await outStream.WriteAsync(buffer.AsMemory(0, read), ct);

                        var downloaded = Interlocked.Add(ref downloadedTotal, read);
                        var speed = downloaded / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                        var remaining = totalBytes - downloaded;
                        var eta = speed > 0 ? TimeSpan.FromSeconds(remaining / speed) : (TimeSpan?)null;

                        progress.Report(new DownloadProgressUpdate
                        {
                            Status = DownloadJobStatus.Downloading,
                            DownloadedBytes = downloaded,
                            TotalBytes = totalBytes,
                            SpeedBytesPerSecond = speed,
                            Eta = eta,
                            ProgressPercent = downloaded * 100d / totalBytes,
                            ActiveSegments = segmentCount,
                            StatusMessage = "Segmented download",
                        });
                    }
                });

            await using var merged = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, useAsync: true);
            foreach (var segment in ranges.OrderBy(r => r.index))
            {
                var segmentFile = outputPath + $".seg{segment.index:00}";
                await using var source = new FileStream(segmentFile, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, useAsync: true);
                await source.CopyToAsync(merged, cancellationToken);
            }
        }
        finally
        {
            foreach (var file in segmentFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Non-fatal cleanup failure.
                }
            }
        }
    }

    private static List<(int index, long start, long end)> BuildRanges(long totalBytes, int segmentCount)
    {
        var ranges = new List<(int index, long start, long end)>();
        var chunk = totalBytes / segmentCount;

        for (var i = 0; i < segmentCount; i++)
        {
            var start = i * chunk;
            var end = i == segmentCount - 1
                ? totalBytes - 1
                : start + chunk - 1;
            ranges.Add((i, start, end));
        }

        return ranges;
    }

    /// <summary>
    /// Returns the shared <see cref="HttpClient"/> for this engine instance, recreating it
    /// only when the relevant connection settings have changed.
    /// Fix Issue 5: prevents per-request HttpClient creation and socket exhaustion.
    /// </summary>
    private HttpClient GetOrCreateClient(DownloaderSettings settings)
    {
        lock (_clientLock)
        {
            if (_httpClient is not null && ReferenceEquals(_lastSettings, settings))
            {
                return _httpClient;
            }

            _httpClient?.Dispose();
            _httpClient = BuildHttpClient(settings);
            _lastSettings = settings;
            return _httpClient;
        }
    }

    private static HttpClient BuildHttpClient(DownloaderSettings settings)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            MaxAutomaticRedirections = Math.Clamp(settings.Connections.MaxRedirects, 1, 20),
            AllowAutoRedirect = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = Math.Clamp(settings.Connections.PerHostConnectionLimit, 1, 20),
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.Connections.TimeoutSeconds, 5, 300)),
        };

        var userAgent = string.IsNullOrWhiteSpace(settings.Connections.UserAgentOverride)
            ? "WindowsUtilityPack.Downloader/2.0"
            : settings.Connections.UserAgentOverride;

        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        ApplyCustomHeaders(client, settings.Connections.CustomHeaders);

        return client;
    }

    private static void ApplyCustomHeaders(HttpClient client, string customHeaders)
    {
        if (string.IsNullOrWhiteSpace(customHeaders))
        {
            return;
        }

        var lines = customHeaders.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var separator = line.IndexOf(':');
            if (separator <= 0 || separator >= line.Length - 1)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                client.DefaultRequestHeaders.Remove(key);
                client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
            }
        }
    }

    private static string GetFileNameFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Path.GetFileName(uri.LocalPath);
        }

        return string.Empty;
    }

    private static string BuildFallbackFileName(string url, string? contentType)
    {
        var urlFileName = GetFileNameFromUrl(url);
        if (!string.IsNullOrWhiteSpace(urlFileName))
        {
            return urlFileName;
        }

        var extension = contentType switch
        {
            "text/html" => ".html",
            "application/json" => ".json",
            "text/plain" => ".txt",
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "audio/mpeg" => ".mp3",
            "video/mp4" => ".mp4",
            _ => ".dat",
        };

        var hostPrefix = "download";
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            hostPrefix = uri.Host.Replace(".", "-", StringComparison.Ordinal);
        }

        return $"{hostPrefix}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}{extension}";
    }
}
