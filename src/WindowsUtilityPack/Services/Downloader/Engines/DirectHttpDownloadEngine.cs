using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private ClientEntry? _currentClient;
    private bool _disposed;
    private readonly object _clientLock = new();

    public override DownloadEngineType EngineType => DownloadEngineType.DirectHttp;

    public void Dispose()
    {
        lock (_clientLock)
        {
            _disposed = true;
            if (_currentClient?.Users == 0) _currentClient.Client.Dispose();
            _currentClient = null;
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

        using var lease = AcquireClient(settings);
        var client = lease.Client;
        using var request = new HttpRequestMessage(HttpMethod.Head, job.SourceUrl);
        string? contentType = null;

        try
        {
            using var response = await SendFollowingRedirectsAsync(client, request, settings, cancellationToken);
            response.EnsureSuccessStatusCode();
            result.ResolvedUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? job.SourceUrl;
            contentType = response.Content.Headers.ContentType?.MediaType;

            if (response.Headers.Location is not null)
            {
                result.ResolvedUrl = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location.ToString()
                    : new Uri(new Uri(job.SourceUrl), response.Headers.Location).ToString();
            }

            result.TotalBytes = response.Content.Headers.ContentLength;
            result.SupportsResume = response.Headers.ETag is { IsWeak: false } &&
                response.Headers.AcceptRanges.Any(range => range.Equals("bytes", StringComparison.OrdinalIgnoreCase));

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
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException)
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
        settings = JsonSerializer.Deserialize<DownloaderSettings>(JsonSerializer.Serialize(settings))!;
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(plan.TargetDirectory);

        var targetFileName = SanitizeFileName(plan.TargetFileName);
        var targetPath = Path.Combine(plan.TargetDirectory, targetFileName);

        FileStream? acquired = null;
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (plan.AutoRenameExisting) targetPath = GetUniqueFilePath(plan.TargetDirectory, targetFileName);
            else if (Directory.Exists(targetPath)) throw new IOException("A directory occupies the download destination.");
            else if (File.Exists(targetPath) && !plan.OverwriteExisting)
            {
                progress.Report(new DownloadProgressUpdate
                {
                    Status = DownloadJobStatus.Skipped, StatusMessage = "Skipped: target file already exists.",
                    ProgressPercent = 100, OutputFilePath = targetPath,
                });
                return;
            }

            try
            {
                acquired = new FileStream(targetPath + ".wup-download.lock", FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException ex) when (plan.AutoRenameExisting &&
                ((ex.HResult & 0xffff) is 80 or 183 or 32 || File.Exists(targetPath + ".wup-download.lock")))
            {
                continue; // A competing job won the reservation after our name selection.
            }
            // A publisher outside this engine can create a file between selection and reservation.
            if (plan.AutoRenameExisting && (File.Exists(targetPath) || Directory.Exists(targetPath)))
            {
                acquired.Dispose(); acquired = null; continue;
            }
            break;
        }
        using var reservation = acquired ?? throw new IOException("Could not reserve a unique download filename.");
        var partPath = settings.FileHandling.UsePartFiles ? targetPath + ".part" : targetPath + $".{Guid.NewGuid():N}.part";
        if (!settings.FileHandling.UsePartFiles) settings.FileHandling.ResumePartialFiles = false;

        var segmentCount = Math.Clamp(job.SegmentCount, 1, 8);
        var canSegment = segmentCount > 1
                         && job.TotalBytes is > 0
                         && job.IsResumable
                         && !File.Exists(partPath);

        using var lease = AcquireClient(settings);
        var client = lease.Client;

        try
        {
            if (canSegment)
                await DownloadSegmentedAsync(job.SourceUrl, partPath, job.TotalBytes!.Value, segmentCount, client, settings, progress, cancellationToken);
            else
                await DownloadSingleAsync(job.SourceUrl, partPath, client, settings, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partPath, targetPath, overwrite: plan.OverwriteExisting && !plan.AutoRenameExisting);
            File.Delete(partPath + ".resume.json");

        progress.Report(new DownloadProgressUpdate
        {
            Status = DownloadJobStatus.Completed,
            StatusMessage = "Download complete.",
            ProgressPercent = 100,
            OutputFilePath = targetPath,
        });
        }
        finally
        {
            if (!settings.FileHandling.UsePartFiles)
            {
                File.Delete(partPath);
                File.Delete(partPath + ".resume.json");
            }
        }
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
        ResumeState? resume = null;
        if (settings.FileHandling.ResumePartialFiles && File.Exists(outputPath))
        {
            resume = await ReadResumeStateAsync(outputPath, cancellationToken);
            var length = new FileInfo(outputPath).Length;
            if (resume is not null && resume.UrlHash == HashUrl(url) && length > 0 && length < resume.TotalBytes &&
                EntityTagHeaderValue.TryParse(resume.ETag, out var tag) && !tag.IsWeak)
                resumeBytes = length;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (resumeBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(resumeBytes, null);
            request.Headers.IfRange = new RangeConditionHeaderValue(new EntityTagHeaderValue(resume!.ETag));
        }

        using var response = await SendFollowingRedirectsAsync(client, request, settings, cancellationToken);
        // Never truncate a partial file or publish an HTTP error body as a download.
        response.EnsureSuccessStatusCode();

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            if (resumeBytes > 0 && (response.Headers.ETag?.ToString() != resume!.ETag ||
                HashUrl(response.RequestMessage?.RequestUri?.AbsoluteUri ?? url) != resume.ResourceHash))
                throw new InvalidDataException("The remote file changed. Restart the download instead of appending to it.");
            var range = response.Content.Headers.ContentRange;
            if (range is null || !string.Equals(range.Unit, "bytes", StringComparison.OrdinalIgnoreCase)
                || range.From != resumeBytes || !range.To.HasValue || !range.Length.HasValue
                || range.To.Value != range.Length.Value - 1)
            {
                throw new InvalidDataException("The server returned an invalid resume range.");
            }
        }

        if (resumeBytes > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            resumeBytes = 0;
        }

        var totalLength = response.Content.Headers.ContentLength;
        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var rangeLength = response.Content.Headers.ContentRange!.To!.Value - resumeBytes + 1;
            if (totalLength.HasValue && totalLength.Value != rangeLength)
                throw new InvalidDataException("The response length does not match the resume range.");
            totalLength = rangeLength;
        }
        var expectedTotal = totalLength.HasValue ? totalLength.Value + resumeBytes : (long?)null;

        var fileMode = resumeBytes > 0 && response.StatusCode == HttpStatusCode.PartialContent
            ? FileMode.Append
            : FileMode.Create;

        if (fileMode == FileMode.Create && File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        await using var output = new FileStream(outputPath, fileMode, FileAccess.Write, FileShare.None, DefaultBufferSize, useAsync: true);
        var statePath = outputPath + ".resume.json";
        if (response.Headers.ETag is { IsWeak: false } etag && expectedTotal is > 0)
        {
            var temporary = statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var state = new ResumeState(HashUrl(url), HashUrl(response.RequestMessage?.RequestUri?.AbsoluteUri ?? url), etag.ToString(), expectedTotal.Value);
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state), cancellationToken);
                File.Move(temporary, statePath, true);
            }
            finally { File.Delete(temporary); }
        }
        else File.Delete(statePath);
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

            var read = await ReadWithTimeoutAsync(stream, buffer, settings, cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

            downloaded += read;
            if (expectedTotal.HasValue && downloaded > expectedTotal) throw new InvalidDataException("The server sent more bytes than advertised.");
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

        if (expectedTotal.HasValue && downloaded != expectedTotal.Value)
            throw new InvalidDataException("The download ended before the expected number of bytes arrived.");

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
        var segmentPrefix = outputPath + "." + Guid.NewGuid().ToString("N");
        var mergePath = segmentPrefix + ".merge";
        long downloadedTotal = 0;
        var stopwatch = Stopwatch.StartNew();
        var ranges = BuildRanges(totalBytes, segmentCount);
        string? entityTag = null;
        string? resourceUrl = null;

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
                    var segmentFile = segmentPrefix + $".seg{range.index:00}";
                    lock (segmentFiles)
                    {
                        segmentFiles.Add(segmentFile);
                    }

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = new RangeHeaderValue(range.start, range.end);

                    using var response = await SendFollowingRedirectsAsync(client, request, settings, ct);
                    response.EnsureSuccessStatusCode();
                    lock (segmentFiles)
                    {
                        var tag = response.Headers.ETag;
                        var resolved = response.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
                        if (tag is null || tag.IsWeak) throw new InvalidDataException("Segmented downloads require a strong ETag. Retry with one segment.");
                        entityTag ??= tag.ToString();
                        resourceUrl ??= resolved;
                        if (entityTag != tag.ToString() || resourceUrl != resolved)
                            throw new InvalidDataException("The remote file changed between segments.");
                    }
                    var contentRange = response.Content.Headers.ContentRange;
                    var expectedLength = range.end - range.start + 1;
                    if (response.StatusCode != HttpStatusCode.PartialContent
                        || contentRange is null
                        || !string.Equals(contentRange.Unit, "bytes", StringComparison.OrdinalIgnoreCase)
                        || contentRange.From != range.start || contentRange.To != range.end
                        || contentRange.Length != totalBytes
                        || (response.Content.Headers.ContentLength.HasValue
                            && response.Content.Headers.ContentLength.Value != expectedLength))
                    {
                        throw new InvalidDataException("The server did not honor the requested byte range. Retry with one segment.");
                    }

                    await using var inStream = await response.Content.ReadAsStreamAsync(ct);
                    await using var outStream = new FileStream(segmentFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, DefaultBufferSize, useAsync: true);
                    var buffer = new byte[DefaultBufferSize];
                    long segmentBytes = 0;

                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        var read = await ReadWithTimeoutAsync(inStream, buffer, settings, ct);
                        if (read <= 0)
                        {
                            break;
                        }

                        segmentBytes += read;
                        if (segmentBytes > expectedLength)
                            throw new InvalidDataException("The server sent more bytes than the requested range.");
                        await outStream.WriteAsync(buffer.AsMemory(0, read), ct);

                        var downloaded = Interlocked.Add(ref downloadedTotal, read);
                        if (settings.Connections.BandwidthLimitKbps > 0)
                        {
                            var delay = TimeSpan.FromSeconds(downloaded / (settings.Connections.BandwidthLimitKbps * 1024d / 8d)) - stopwatch.Elapsed;
                            if (delay > TimeSpan.FromMilliseconds(25)) await Task.Delay(delay, ct);
                        }
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
                    if (segmentBytes != expectedLength)
                        throw new InvalidDataException("A download segment was incomplete.");
                });

            await using (var merged = new FileStream(mergePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, DefaultBufferSize, useAsync: true))
            {
            foreach (var segment in ranges.OrderBy(r => r.index))
            {
                var segmentFile = segmentPrefix + $".seg{segment.index:00}";
                await using var source = new FileStream(segmentFile, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, useAsync: true);
                await source.CopyToAsync(merged, cancellationToken);
            }
            await merged.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(outputPath + ".resume.json");
            File.Move(mergePath, outputPath, true);
        }
        finally
        {
            File.Delete(mergePath);
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
        segmentCount = (int)Math.Min(segmentCount, totalBytes);
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
    private ClientLease AcquireClient(DownloaderSettings settings)
    {
        var key = JsonSerializer.Serialize(settings.Connections);
        lock (_clientLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_currentClient is null || _currentClient.Key != key)
            {
                var next = new ClientEntry(BuildHttpClient(settings), key);
                if (_currentClient?.Users == 0) _currentClient.Client.Dispose();
                _currentClient = next;
            }
            var entry = _currentClient;
            entry.Users++;
            return new ClientLease(entry.Client, () =>
            {
                lock (_clientLock)
                    if (--entry.Users == 0 && !ReferenceEquals(entry, _currentClient)) entry.Client.Dispose();
            });
        }
    }

    private sealed class ClientEntry(HttpClient client, string key)
    {
        public HttpClient Client { get; } = client;
        public string Key { get; } = key;
        public int Users { get; set; }
    }
    private sealed class ClientLease(HttpClient client, Action release) : IDisposable
    {
        public HttpClient Client { get; } = client;
        public void Dispose() => release();
    }
    private sealed record ResumeState(string UrlHash, string ResourceHash, string ETag, long TotalBytes);
    private static string HashUrl(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
    private static async Task<ResumeState?> ReadResumeStateAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path + ".resume.json");
            if (!info.Exists || info.Length > 16384) return null;
            return JsonSerializer.Deserialize<ResumeState>(await File.ReadAllTextAsync(info.FullName, cancellationToken));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
    private static async Task<int> ReadWithTimeoutAsync(Stream stream, Memory<byte> buffer, DownloaderSettings settings, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.Connections.TimeoutSeconds, 5, 300)));
        return await stream.ReadAsync(buffer, deadline.Token);
    }
    private static HttpClient BuildHttpClient(DownloaderSettings settings)
    {
        var handler = new SocketsHttpHandler
        {
            // Byte ranges and file lengths refer to the transferred representation.
            AutomaticDecompression = DecompressionMethods.None,
            MaxAutomaticRedirections = Math.Clamp(settings.Connections.MaxRedirects, 1, 20),
            AllowAutoRedirect = false,
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

        return client;
    }

    private static async Task<HttpResponseMessage> SendFollowingRedirectsAsync(HttpClient client, HttpRequestMessage original,
        DownloaderSettings settings, CancellationToken cancellationToken)
    {
        var initial = original.RequestUri ?? throw new InvalidOperationException("A download URL is required.");
        var uri = initial;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.Connections.TimeoutSeconds, 5, 300)));
        for (var hop = 0; ; hop++)
        {
            if (uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidDataException("Downloads require HTTP(S) URLs without embedded credentials.");
            using var request = new HttpRequestMessage(original.Method, uri);
            foreach (var header in original.Headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (uri.Scheme == initial.Scheme && uri.IdnHost == initial.IdnHost && uri.Port == initial.Port)
                ApplyCustomHeaders(request, settings.Connections.CustomHeaders);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
                return response;
            try
            {
                if (hop >= Math.Clamp(settings.Connections.MaxRedirects, 0, 20)) throw new HttpRequestException("The download exceeded the redirect limit.");
                var location = response.Headers.Location ?? throw new HttpRequestException("The redirect has no destination.");
                var next = location.IsAbsoluteUri ? location : new Uri(uri, location);
                if (uri.Scheme == "https" && next.Scheme != "https") throw new HttpRequestException("An HTTPS download redirected to an insecure URL.");
                uri = next;
            }
            finally { response.Dispose(); }
        }
    }

    private static void ApplyCustomHeaders(HttpRequestMessage request, string customHeaders)
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
                request.Headers.Remove(key);
                request.Headers.TryAddWithoutValidation(key, value);
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
