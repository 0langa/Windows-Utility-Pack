using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Services.Downloader.Engines;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class DirectHttpDownloadEngineTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wup-http-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Payload = Encoding.ASCII.GetBytes("0123456789ABCDEF");

    public DirectHttpDownloadEngineTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task AutoRename_DirectoryCollisionUsesNextName()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "same.bin"));
        await using var server = new LocalServer(_ => new Reply(200, Payload));
        using var engine = new DirectHttpDownloadEngine();
        await engine.ExecuteAsync(Job(server), new(), new() { TargetDirectory = _directory, TargetFileName = "same.bin", AutoRenameExisting = true }, new Recorder(), default);
        Assert.True(Directory.Exists(Path.Combine(_directory, "same.bin")));
        Assert.Equal(Payload, File.ReadAllBytes(Path.Combine(_directory, "same (1).bin")));
    }

    [Fact]
    public async Task AutoRename_ConcurrentSameBasenameJobsPublishBothDistinctPayloads()
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var firstServer = new LocalServer(_ => { received.TrySetResult(); return new Reply(200, Encoding.ASCII.GetBytes("first"), HeaderDelayMs: 500); });
        await using var secondServer = new LocalServer(_ => new Reply(200, Encoding.ASCII.GetBytes("second")));
        using var engine = new DirectHttpDownloadEngine();
        var plan = new DownloadExecutionPlan { TargetDirectory = _directory, TargetFileName = "same.bin", AutoRenameExisting = true };
        var first = engine.ExecuteAsync(Job(firstServer), new(), plan, new Recorder(), default);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = engine.ExecuteAsync(Job(secondServer), new(), plan, new Recorder(), default);
        await Task.WhenAll(first, second);
        Assert.Equal("first", File.ReadAllText(Path.Combine(_directory, "same.bin")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(_directory, "same (1).bin")));
        Assert.Empty(Directory.GetFiles(_directory, "*.wup-download.lock"));
    }

    [Fact]
    public async Task Resume_WithoutEntityValidatorRestartsInsteadOfMixingVersions()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "download.bin.part"), "OLD DATA");
        await using var server = new LocalServer(range => range is null
            ? new Reply(200, Payload) : new Reply(206, Payload[8..], "bytes 8-15/16"));
        using var engine = new DirectHttpDownloadEngine();
        await engine.ExecuteAsync(Job(server), new(), Plan(), new Recorder(), CancellationToken.None);
        Assert.Equal(Payload, await File.ReadAllBytesAsync(Path.Combine(_directory, "download.bin")));
    }

    [Fact]
    public async Task SegmentedDownload_RejectsDifferentEntityVersions()
    {
        await using var server = new LocalServer(range =>
        {
            var start = int.Parse(range![6..].Split('-')[0]);
            return new Reply(206, Payload[start..(start + 8)], $"bytes {start}-{start + 7}/16", ETag: start == 0 ? "\"v1\"" : "\"v2\"");
        });
        var job = Job(server); job.SegmentCount = 2; job.IsResumable = true; job.TotalBytes = 16;
        using var engine = new DirectHttpDownloadEngine();
        await Assert.ThrowsAsync<InvalidDataException>(() => engine.ExecuteAsync(job, new(), Plan(), new Recorder(), CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(_directory, "download.bin")));
    }

    [Fact]
    public async Task FailedDownload_PreservesExistingTargetEvenWithPartFilesDisabled()
    {
        var target = Path.Combine(_directory, "download.bin");
        await File.WriteAllTextAsync(target, "keep original");
        await using var server = new LocalServer(_ => new Reply(200, Payload, DeclaredLength: 32));
        var settings = new DownloaderSettings(); settings.FileHandling.UsePartFiles = false; settings.FileHandling.ResumePartialFiles = false;
        using var engine = new DirectHttpDownloadEngine();
        await Assert.ThrowsAnyAsync<Exception>(() => engine.ExecuteAsync(Job(server), settings,
            new() { TargetDirectory = _directory, TargetFileName = "download.bin", OverwriteExisting = true, AutoRenameExisting = false }, new Recorder(), CancellationToken.None));
        Assert.Equal("keep original", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task ConcurrentJobs_WithDifferentSettingsKeepTheirClientsAlive()
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new LocalServer(_ => { received.TrySetResult(); return new Reply(200, Payload, HeaderDelayMs: 150); });
        using var engine = new DirectHttpDownloadEngine();
        var first = engine.ExecuteAsync(Job(server), new(), Plan(), new Recorder(), CancellationToken.None);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var secondSettings = new DownloaderSettings(); secondSettings.Connections.UserAgentOverride = "Fixture/2.0";
        var second = engine.ExecuteAsync(Job(server), secondSettings,
            new() { TargetDirectory = _directory, TargetFileName = "second.bin" }, new Recorder(), CancellationToken.None);
        await Task.WhenAll(first, second);
        Assert.Equal(Payload, await File.ReadAllBytesAsync(Path.Combine(_directory, "download.bin")));
        Assert.Equal(Payload, await File.ReadAllBytesAsync(Path.Combine(_directory, "second.bin")));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    public async Task HttpFailure_DoesNotReplacePartialOrPublishCompletedFile(int status)
    {
        await using var server = new LocalServer(_ => new Reply(status, Payload));
        await File.WriteAllTextAsync(Path.Combine(_directory, "download.bin.part"), "keep");
        using var engine = new DirectHttpDownloadEngine();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var progress = new Recorder();

        await Assert.ThrowsAsync<HttpRequestException>(() => engine.ExecuteAsync(Job(server), new DownloaderSettings(), Plan(), progress, deadline.Token));

        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(_directory, "download.bin.part")));
        Assert.False(File.Exists(Path.Combine(_directory, "download.bin")));
        Assert.DoesNotContain(progress.Updates, update => update.Status == DownloadJobStatus.Completed);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("ignored")]
    [InlineData("wrong-offset")]
    [InlineData("short-body")]
    [InlineData("long-body")]
    [InlineData("missing-range")]
    public async Task SegmentedDownload_ValidatesResponseAndBytes(string scenario)
    {
        await using var server = new LocalServer(range =>
        {
            Assert.NotNull(range);
            var bounds = range![6..].Split('-');
            var start = int.Parse(bounds[0]);
            var end = int.Parse(bounds[1]);
            var bytes = Payload[start..(end + 1)];
            return scenario switch
            {
                "ignored" => new Reply(200, Payload),
                "wrong-offset" => new Reply(206, bytes, $"bytes {start + 1}-{end + 1}/{Payload.Length}"),
                "short-body" => new Reply(206, bytes[..^1], $"bytes {start}-{end}/{Payload.Length}", OmitLength: true),
                "long-body" => new Reply(206, [.. bytes, 1], $"bytes {start}-{end}/{Payload.Length}", OmitLength: true),
                "missing-range" => new Reply(206, bytes),
                _ => new Reply(206, bytes, $"bytes {start}-{end}/{Payload.Length}"),
            };
        });
        using var engine = new DirectHttpDownloadEngine();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var progress = new Recorder();
        var job = Job(server);
        job.SegmentCount = 2;
        job.IsResumable = true;
        job.TotalBytes = Payload.Length;
        var download = engine.ExecuteAsync(job, new DownloaderSettings(), Plan(), progress, deadline.Token);
        if (scenario == "valid")
        {
            await download;
            Assert.Equal(Payload, await File.ReadAllBytesAsync(Path.Combine(_directory, "download.bin")));
            Assert.Contains(progress.Updates, update => update.Status == DownloadJobStatus.Completed);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => download);
            Assert.False(File.Exists(Path.Combine(_directory, "download.bin")));
            Assert.DoesNotContain(progress.Updates, update => update.Status == DownloadJobStatus.Completed);
        }
        Assert.Empty(Directory.GetFiles(_directory, "*.seg*"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resume_RequiresCorrectRangeStart(bool wrongStart)
    {
        await File.WriteAllBytesAsync(Path.Combine(_directory, "download.bin.part"), Payload[..8]);
        await using var server = new LocalServer(range =>
        {
            Assert.Equal("bytes=8-", range);
            return wrongStart
                ? new Reply(206, Payload, "bytes 0-15/16")
                : new Reply(206, Payload[8..], "bytes 8-15/16");
        });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(server.Url)));
        await File.WriteAllTextAsync(Path.Combine(_directory, "download.bin.part.resume.json"),
            System.Text.Json.JsonSerializer.Serialize(new { UrlHash = hash, ResourceHash = hash, ETag = "\"fixture\"", TotalBytes = 16L }));
        using var engine = new DirectHttpDownloadEngine();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var download = engine.ExecuteAsync(Job(server), new DownloaderSettings(), Plan(), new Recorder(), deadline.Token);
        if (wrongStart)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => download);
            Assert.Equal(Payload[..8], await File.ReadAllBytesAsync(Path.Combine(_directory, "download.bin.part")));
        }
        else
        {
            await download;
            Assert.Equal(Payload, await File.ReadAllBytesAsync(Path.Combine(_directory, "download.bin")));
        }
    }

    [Fact]
    public async Task SegmentedDownload_FileSmallerThanSegmentCount_DoesNotRequestNegativeRanges()
    {
        await using var server = new LocalServer(range =>
        {
            Assert.Equal("bytes=0-0", range);
            return new Reply(206, [42], "bytes 0-0/1");
        });
        using var engine = new DirectHttpDownloadEngine();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var job = Job(server);
        job.SegmentCount = 8;
        job.IsResumable = true;
        job.TotalBytes = 1;
        await engine.ExecuteAsync(job, new DownloaderSettings(), Plan(), new Recorder(), deadline.Token);
        Assert.Equal(new byte[] { 42 }, await File.ReadAllBytesAsync(Path.Combine(_directory, "download.bin")));
    }

    [Fact]
    public async Task Redirect_DoesNotForwardCustomCredentialsToAnotherOrigin()
    {
        await using var target = new LocalServer(_ => new Reply(200, Payload));
        await using var redirect = new LocalServer(_ => new Reply(302, [], Location: target.Url));
        using var engine = new DirectHttpDownloadEngine();
        var settings = new DownloaderSettings();
        settings.Connections.CustomHeaders = "Cookie: private-cookie\r\nAuthorization: Bearer private-token\r\nX-Api-Key: private-key";
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await engine.ExecuteAsync(Job(redirect), settings, Plan(), new Recorder(), deadline.Token);
        var initial = Assert.Single(redirect.RequestHeaders);
        Assert.Contains("private-cookie", initial);
        Assert.DoesNotContain("private-", Assert.Single(target.RequestHeaders));
        Assert.Equal(Payload, await File.ReadAllBytesAsync(Path.Combine(_directory, "download.bin")));
    }

    private static DownloadJob Job(LocalServer server) => new() { SourceUrl = server.Url, SegmentCount = 1 };
    private DownloadExecutionPlan Plan() => new() { TargetDirectory = _directory, TargetFileName = "download.bin" };
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class Recorder : IProgress<DownloadProgressUpdate>
    {
        public ConcurrentBag<DownloadProgressUpdate> Updates { get; } = [];
        public void Report(DownloadProgressUpdate value) => Updates.Add(value);
    }

    private sealed record Reply(int Status, byte[] Body, string? ContentRange = null, bool OmitLength = false,
        string ETag = "\"fixture\"", int? DeclaredLength = null, int HeaderDelayMs = 0, string? Location = null);

    // Real loopback HTTP, with no external service or administrator URL reservation.
    private sealed class LocalServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(20));
        private readonly List<Task> _requests = [];
        private readonly Task _acceptLoop;
        public string Url { get; }
        public ConcurrentBag<string> RequestHeaders { get; } = [];

        public LocalServer(Func<string?, Reply> response)
        {
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/fixture";
            _acceptLoop = AcceptAsync(response);
        }

        private async Task AcceptAsync(Func<string?, Reply> response)
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _requests.Add(HandleAsync(client, response));
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (SocketException) when (_stop.IsCancellationRequested) { }
        }

        private async Task HandleAsync(TcpClient client, Func<string?, Reply> response)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    string? range = null;
                    var receivedHeaders = new StringBuilder();
                    while (true)
                    {
                        var line = await reader.ReadLineAsync(_stop.Token);
                        // A sibling segment may cancel this connection before its
                        // complete HTTP headers arrive. EOF is not a request.
                        if (line is null) return;
                        if (line.Length == 0) break;
                        receivedHeaders.AppendLine(line);
                        if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) range = line[6..].Trim();
                    }
                    RequestHeaders.Add(receivedHeaders.ToString());
                    var reply = response(range);
                    if (reply.HeaderDelayMs > 0) await Task.Delay(reply.HeaderDelayMs, _stop.Token);
                    var headers = $"HTTP/1.1 {reply.Status} Fixture\r\nConnection: close\r\n";
                    if (!reply.OmitLength) headers += $"Content-Length: {reply.DeclaredLength ?? reply.Body.Length}\r\n";
                    headers += $"ETag: {reply.ETag}\r\n";
                    if (reply.Location is not null) headers += $"Location: {reply.Location}\r\n";
                    if (reply.ContentRange is not null) headers += $"Content-Range: {reply.ContentRange}\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(headers + "\r\n"), _stop.Token);
                    await stream.WriteAsync(reply.Body, _stop.Token);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
                catch (IOException) when (_stop.IsCancellationRequested) { }
                // Rejecting an invalid response can close the peer before its body is written.
                catch (IOException ex) when (ex.InnerException is SocketException
                    { SocketErrorCode: SocketError.ConnectionAborted or SocketError.ConnectionReset or SocketError.Shutdown }) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            await _acceptLoop;
            await Task.WhenAll(_requests);
            _stop.Dispose();
        }
    }
}
