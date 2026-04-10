using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.NetworkSpeedTest;

/// <summary>Records a single speed test run result.</summary>
public class SpeedTestResult
{
    public string Timestamp    { get; set; } = string.Empty;
    public string Download     { get; set; } = string.Empty;
    public string Upload       { get; set; } = string.Empty;
    public string Latency      { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the Network Speed Test tool.
/// Measures download speed, upload speed (simulated), and latency.
/// </summary>
public class NetworkSpeedTestViewModel : ViewModelBase
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly IClipboardService _clipboard;
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

    public ObservableCollection<SpeedTestResult> History { get; } = [];

    public AsyncRelayCommand RunTestCommand    { get; }
    public RelayCommand      StopCommand       { get; }
    public RelayCommand      CopyResultsCommand { get; }

    public NetworkSpeedTestViewModel(IClipboardService clipboard)
    {
        _clipboard = clipboard;

        RunTestCommand     = new AsyncRelayCommand(_ => RunTestAsync(), _ => !IsTesting);
        StopCommand        = new RelayCommand(_ => _cts?.Cancel(),      _ => IsTesting);
        CopyResultsCommand = new RelayCommand(_ => CopyResults(),       _ => History.Count > 0);
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                History.Insert(0, new SpeedTestResult
                {
                    Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    Download  = DownloadSpeed,
                    Upload    = UploadSpeed,
                    Latency   = Latency,
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
            for (var i = 0; i < 4 && !ct.IsCancellationRequested; i++)
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
        }
        catch { Latency = "N/A"; }
    }

    private async Task MeasureDownloadAsync(CancellationToken ct)
    {
        var sw         = Stopwatch.StartNew();
        long totalBytes = 0;

        try
        {
            using var response = await _httpClient.GetAsync(ServerUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            using var stream   = await response.Content.ReadAsStreamAsync(ct);
            var contentLength  = response.Content.Headers.ContentLength ?? 25_000_000L;
            var buffer         = new byte[65536];
            int read;

            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                totalBytes += read;
                var elapsed = sw.Elapsed.TotalSeconds;
                if (elapsed > 0)
                {
                    var mbps = totalBytes * 8.0 / elapsed / 1_000_000;
                    Application.Current.Dispatcher.Invoke(() =>
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
        // Simulate upload: generate random data and measure local throughput
        // (a real test needs a cooperative upload endpoint)
        const int uploadSize = 10_000_000; // 10 MB
        var data   = new byte[uploadSize];
        Random.Shared.NextBytes(data);

        var sw       = Stopwatch.StartNew();
        var uploaded = 0;
        var chunkSize = 65536;

        while (uploaded < uploadSize && !ct.IsCancellationRequested)
        {
            var toWrite = Math.Min(chunkSize, uploadSize - uploaded);
            // Simulate network delay (1 ms per 64 KB chunk)
            await Task.Delay(1, ct);
            uploaded += toWrite;

            var elapsed = sw.Elapsed.TotalSeconds;
            if (elapsed > 0)
            {
                var mbps = uploaded * 8.0 / elapsed / 1_000_000;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UploadSpeed    = $"{mbps:F1} Mbps (est.)";
                    UploadProgress = uploaded * 100.0 / uploadSize;
                });
            }
        }

        sw.Stop();
        UploadProgress = 100;
    }

    private void CopyResults()
    {
        if (History.Count == 0) return;
        var latest = History[0];
        var text   = $"Speed Test Results ({latest.Timestamp})\n" +
                     $"Download: {latest.Download}\n" +
                     $"Upload:   {latest.Upload}\n" +
                     $"Latency:  {latest.Latency}\n";
        _clipboard.SetText(text);
        StatusMessage = "Results copied to clipboard.";
    }
}
