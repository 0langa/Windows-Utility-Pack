using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.NetworkSpeedTest;

/// <summary>Records a single speed test run result.</summary>
public class SpeedTestResult
{
    public string Profile      { get; set; } = string.Empty;
    public string Timestamp    { get; set; } = string.Empty;
    public string Download     { get; set; } = string.Empty;
    public string Upload       { get; set; } = string.Empty;
    public string Latency      { get; set; } = string.Empty;
    public string Jitter       { get; set; } = string.Empty;
    public string PacketLoss   { get; set; } = string.Empty;
}

/// <summary>Represents a reusable speed test profile preset.</summary>
public sealed class SpeedTestProfile
{
    public required string Name { get; init; }
    public required string DownloadUrl { get; init; }
    public required string UploadUrl { get; init; }
    public required long DownloadBytesHint { get; init; }
    public required long UploadBytesHint { get; init; }
    public int PingSamples { get; init; } = 6;

    public override string ToString() => Name;
}

/// <summary>
/// ViewModel for the Network Speed Test tool.
/// Measures download speed, upload speed, and latency.
/// </summary>
public class NetworkSpeedTestViewModel : ViewModelBase
{
    private static readonly SocketsHttpHandler HttpHandler = new()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    };

    private static readonly HttpClient SharedHttpClient = new(HttpHandler)
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private const string DefaultUploadUrl = "https://speed.cloudflare.com/__up";

    private readonly IClipboardService _clipboard;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource?   _cts;

    private string _downloadSpeed   = "-- Mbps";
    private string _uploadSpeed     = "-- Mbps";
    private string _latency         = "-- ms";
    private string _serverUrl       = "https://speed.cloudflare.com/__down?bytes=25000000";
    private double _downloadProgress;
    private double _uploadProgress;
    private bool   _isTesting;
    private bool   _downloadComplete;
    private bool   _uploadComplete;
    private string _statusMessage   = string.Empty;
    private string _latencyJitter = "-- ms";
    private string _packetLoss = "-- %";
    private string _methodologySummary = string.Empty;
    private SpeedTestProfile? _selectedProfile;

    public IReadOnlyList<SpeedTestProfile> Profiles { get; } =
    [
        new()
        {
            Name = "Balanced (Cloudflare 25 MB / 10 MB)",
            DownloadUrl = "https://speed.cloudflare.com/__down?bytes=25000000",
            UploadUrl = "https://speed.cloudflare.com/__up",
            DownloadBytesHint = 25_000_000,
            UploadBytesHint = 10_000_000,
            PingSamples = 6,
        },
        new()
        {
            Name = "Quick Check (Cloudflare 10 MB / 5 MB)",
            DownloadUrl = "https://speed.cloudflare.com/__down?bytes=10000000",
            UploadUrl = "https://speed.cloudflare.com/__up",
            DownloadBytesHint = 10_000_000,
            UploadBytesHint = 5_000_000,
            PingSamples = 4,
        },
        new()
        {
            Name = "High Throughput (Cloudflare 100 MB / 25 MB)",
            DownloadUrl = "https://speed.cloudflare.com/__down?bytes=100000000",
            UploadUrl = "https://speed.cloudflare.com/__up",
            DownloadBytesHint = 100_000_000,
            UploadBytesHint = 25_000_000,
            PingSamples = 8,
        },
    ];

    public string DownloadSpeed
    {
        get => _downloadSpeed;
        set => SetProperty(ref _downloadSpeed, value);
    }

    public string UploadSpeed
    {
        get => _uploadSpeed;
        set => SetProperty(ref _uploadSpeed, value);
    }

    public string Latency
    {
        get => _latency;
        set => SetProperty(ref _latency, value);
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set => SetProperty(ref _serverUrl, value);
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    public double UploadProgress
    {
        get => _uploadProgress;
        set => SetProperty(ref _uploadProgress, value);
    }

    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    public bool DownloadComplete
    {
        get => _downloadComplete;
        set => SetProperty(ref _downloadComplete, value);
    }

    public bool UploadComplete
    {
        get => _uploadComplete;
        set => SetProperty(ref _uploadComplete, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string LatencyJitter
    {
        get => _latencyJitter;
        set => SetProperty(ref _latencyJitter, value);
    }

    public string PacketLoss
    {
        get => _packetLoss;
        set => SetProperty(ref _packetLoss, value);
    }

    public string MethodologySummary
    {
        get => _methodologySummary;
        set => SetProperty(ref _methodologySummary, value);
    }

    public SpeedTestProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value) || value is null)
            {
                return;
            }

            ServerUrl = value.DownloadUrl;
            MethodologySummary = BuildMethodologySummary(value);
        }
    }

    public ObservableCollection<SpeedTestResult> History { get; } = [];

    public AsyncRelayCommand RunTestCommand    { get; }
    public RelayCommand      StopCommand       { get; }
    public RelayCommand      CopyResultsCommand { get; }

    public NetworkSpeedTestViewModel(IClipboardService clipboard)
        : this(clipboard, SharedHttpClient)
    {
    }

    internal NetworkSpeedTestViewModel(IClipboardService clipboard, HttpClient httpClient)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        RunTestCommand     = new AsyncRelayCommand(_ => RunTestAsync(), _ => !IsTesting);
        StopCommand        = new RelayCommand(_ => _cts?.Cancel(),      _ => IsTesting);
        CopyResultsCommand = new RelayCommand(_ => CopyResults(),       _ => History.Count > 0);

        SelectedProfile = Profiles.FirstOrDefault();
    }

    private async Task RunTestAsync()
    {
        _cts = new CancellationTokenSource();
        IsTesting        = true;
        DownloadComplete = false;
        UploadComplete   = false;
        DownloadProgress = 0;
        UploadProgress   = 0;
        DownloadSpeed    = "-- Mbps";
        UploadSpeed      = "-- Mbps";
        Latency          = "-- ms";
        LatencyJitter    = "-- ms";
        PacketLoss       = "-- %";
        StatusMessage    = "Starting test…";

        var ct = _cts.Token;

        try
        {
            // Step 1: Latency
            StatusMessage = "Measuring latency…";
            await MeasureLatencyAsync(ct);
            if (ct.IsCancellationRequested) return;

            // Step 2: Download
            StatusMessage = "Measuring download speed…";
            await MeasureDownloadAsync(ct);
            if (ct.IsCancellationRequested) return;
            DownloadComplete = true;

            // Step 3: Upload (simulated)
            StatusMessage = "Measuring upload speed…";
            await MeasureUploadAsync(ct);
            UploadComplete = true;

            // Record result
            RunOnUi(() =>
            {
                History.Insert(0, new SpeedTestResult
                {
                    Profile = SelectedProfile?.Name ?? "Custom",
                    Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    Download  = DownloadSpeed,
                    Upload    = UploadSpeed,
                    Latency   = Latency,
                    Jitter    = LatencyJitter,
                    PacketLoss = PacketLoss,
                });
                // Keep last 10
                while (History.Count > 10) History.RemoveAt(History.Count - 1);
            });

            StatusMessage = "Test complete.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Test stopped.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
            _cts      = null;
        }
    }

    private async Task MeasureLatencyAsync(CancellationToken ct)
    {
        try
        {
            var uri  = new Uri(ServerUrl);
            var host = uri.Host;
            using var ping = new Ping();
            var results = new List<long>();
            var samples = Math.Clamp(SelectedProfile?.PingSamples ?? 6, 2, 20);
            for (var i = 0; i < samples && !ct.IsCancellationRequested; i++)
            {
                try
                {
                    var reply = await ping.SendPingAsync(host, 2000);
                    if (reply.Status == IPStatus.Success) results.Add(reply.RoundtripTime);
                }
                catch { /* ignore */ }
            }
            Latency = results.Count > 0
                ? $"{results.Average():F0} ms"
                : "N/A";

            if (results.Count > 1)
            {
                var avg = results.Average();
                var variance = results.Sum(v => Math.Pow(v - avg, 2)) / results.Count;
                LatencyJitter = $"{Math.Sqrt(variance):F0} ms";
            }
            else
            {
                LatencyJitter = "N/A";
            }

            var packetLossPct = 100.0 * Math.Max(0, samples - results.Count) / samples;
            PacketLoss = $"{packetLossPct:F0}%";
        }
        catch
        {
            Latency = "N/A";
            LatencyJitter = "N/A";
            PacketLoss = "N/A";
        }
    }

    private async Task MeasureDownloadAsync(CancellationToken ct)
    {
        var sw         = Stopwatch.StartNew();
        long totalBytes = 0;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
            using var response = await _httpClient.GetAsync(ServerUrl, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            var contentLength  = response.Content.Headers.ContentLength ?? (SelectedProfile?.DownloadBytesHint ?? 25_000_000L);
            var buffer         = new byte[65536];
            int read;

            while ((read = await stream.ReadAsync(buffer, timeoutCts.Token)) > 0)
            {
                totalBytes += read;
                var elapsed = sw.Elapsed.TotalSeconds;
                if (elapsed > 0)
                {
                    var mbps = totalBytes * 8.0 / elapsed / 1_000_000;
                    RunOnUi(() =>
                    {
                        DownloadSpeed    = $"{mbps:F1} Mbps";
                        DownloadProgress = Math.Min(100, totalBytes * 100.0 / contentLength);
                    });
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            DownloadSpeed = $"Error: {ex.Message}";
        }

        sw.Stop();
        if (totalBytes > 0 && sw.Elapsed.TotalSeconds > 0)
        {
            var mbps = totalBytes * 8.0 / sw.Elapsed.TotalSeconds / 1_000_000;
            DownloadSpeed    = $"{mbps:F1} Mbps";
            DownloadProgress = 100;
        }
    }

    private async Task MeasureUploadAsync(CancellationToken ct)
    {
        var uploadSizeBytes = (int)Math.Clamp(SelectedProfile?.UploadBytesHint ?? 10_000_000L, 1_000_000L, 100_000_000L);
        var uploadEndpoint = ResolveUploadEndpoint();
        var sw = Stopwatch.StartNew();
        long uploadedBytes = 0;

        void OnProgress(long bytesWritten)
        {
            uploadedBytes = bytesWritten;
            var elapsedSeconds = sw.Elapsed.TotalSeconds;
            if (elapsedSeconds <= 0)
            {
                return;
            }

            var mbps = bytesWritten * 8.0 / elapsedSeconds / 1_000_000;
            RunOnUi(() =>
            {
                UploadSpeed = $"{mbps:F1} Mbps";
                UploadProgress = Math.Min(100, bytesWritten * 100.0 / uploadSizeBytes);
            });
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
            using var request = new HttpRequestMessage(HttpMethod.Post, uploadEndpoint)
            {
                // Stream random bytes to measure real request-body throughput
                // instead of approximating from a single buffered payload round-trip.
                Content = new UploadMeasurementContent(uploadSizeBytes, 64 * 1024, OnProgress),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UploadSpeed = $"Not measured ({ex.Message})";
            UploadProgress = 0;
            return;
        }

        sw.Stop();
        var elapsedSeconds = sw.Elapsed.TotalSeconds <= 0 ? 0.001 : sw.Elapsed.TotalSeconds;
        if (uploadedBytes <= 0)
        {
            uploadedBytes = uploadSizeBytes;
        }

        var mbps = uploadedBytes * 8.0 / elapsedSeconds / 1_000_000;
        UploadSpeed = $"{mbps:F1} Mbps";
        UploadProgress = 100;
    }

    private string ResolveUploadEndpoint()
    {
        if (SelectedProfile is not null && Uri.TryCreate(SelectedProfile.UploadUrl, UriKind.Absolute, out _))
        {
            return SelectedProfile.UploadUrl;
        }

        if (Uri.TryCreate(ServerUrl, UriKind.Absolute, out var sourceUri)
            && sourceUri.Host.Contains("speed.cloudflare.com", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultUploadUrl;
        }

        return DefaultUploadUrl;
    }

    private sealed class UploadMeasurementContent : HttpContent
    {
        private readonly long _totalBytes;
        private readonly int _chunkSize;
        private readonly Action<long> _progress;

        public UploadMeasurementContent(long totalBytes, int chunkSize, Action<long> progress)
        {
            _totalBytes = totalBytes;
            _chunkSize = chunkSize;
            _progress = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[_chunkSize];
            long written = 0;

            while (written < _totalBytes)
            {
                var toWrite = (int)Math.Min(_chunkSize, _totalBytes - written);
                RandomNumberGenerator.Fill(buffer.AsSpan(0, toWrite));
                await stream.WriteAsync(buffer.AsMemory(0, toWrite));
                written += toWrite;
                _progress(written);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _totalBytes;
            return true;
        }
    }

    private void CopyResults()
    {
        if (History.Count == 0) return;
        var latest = History[0];
        var text   = $"Speed Test Results ({latest.Timestamp})\n" +
                     $"Profile:  {latest.Profile}\n" +
                     $"Download: {latest.Download}\n" +
                     $"Upload:   {latest.Upload}\n" +
                     $"Latency:  {latest.Latency}\n" +
                     $"Jitter:   {latest.Jitter}\n" +
                     $"Loss:     {latest.PacketLoss}\n";
        _clipboard.SetText(text);
        StatusMessage = "Results copied to clipboard.";
    }

    private static string BuildMethodologySummary(SpeedTestProfile profile)
    {
        var down = profile.DownloadBytesHint / 1_000_000d;
        var up = profile.UploadBytesHint / 1_000_000d;
        return $"Method: ICMP latency ({profile.PingSamples} samples), streamed HTTP download (~{down:F0} MB), streamed HTTP upload (~{up:F0} MB).";
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
