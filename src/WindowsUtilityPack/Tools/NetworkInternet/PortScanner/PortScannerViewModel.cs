using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.PortScanner;

/// <summary>Represents a scanned port result.</summary>
public class PortResult
{
    public int    Port        { get; set; }
    public string Status      { get; set; } = string.Empty;
    public string Service     { get; set; } = string.Empty;
    public long   ResponseMs  { get; set; }
    public bool   IsOpen      { get; set; }
}

/// <summary>
/// ViewModel for the Port Scanner tool.
/// Scans a range of TCP ports concurrently with configurable timeout and concurrency.
/// </summary>
public class PortScannerViewModel : ViewModelBase, IDisposable
{
    internal const int MaxSafeConcurrency = 100;

    private static readonly Dictionary<int, string> WellKnownServices = new()
    {
        { 21,    "FTP"        },
        { 22,    "SSH"        },
        { 23,    "Telnet"     },
        { 25,    "SMTP"       },
        { 53,    "DNS"        },
        { 80,    "HTTP"       },
        { 110,   "POP3"       },
        { 143,   "IMAP"       },
        { 443,   "HTTPS"      },
        { 3306,  "MySQL"      },
        { 3389,  "RDP"        },
        { 5432,  "PostgreSQL" },
        { 8080,  "HTTP-Alt"   },
        { 27017, "MongoDB"    },
    };

    private readonly IClipboardService _clipboard;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private Guid? _scanTaskId;
    private readonly Func<string, int, TimeSpan, CancellationToken, Task<bool>> _probe;

    private string _host           = "localhost";
    private string _portRangeText  = "1-1024";
    private int    _timeoutMs      = 300;
    private int    _concurrency    = 50;
    private bool   _isScanning;
    private double _progress;
    private string _statusMessage  = string.Empty;

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public string PortRangeText
    {
        get => _portRangeText;
        set => SetProperty(ref _portRangeText, value);
    }

    public int TimeoutMs
    {
        get => _timeoutMs;
        set => SetProperty(ref _timeoutMs, Math.Clamp(value, 50, 10000));
    }

    public int Concurrency
    {
        get => _concurrency;
        set => SetProperty(ref _concurrency, Math.Clamp(value, 1, MaxSafeConcurrency));
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<PortResult> Results { get; } = [];

    public AsyncRelayCommand ScanCommand          { get; }
    public RelayCommand      StopCommand          { get; }
    public RelayCommand      CopyOpenPortsCommand { get; }

    public PortScannerViewModel(IClipboardService clipboard, IBackgroundTaskService backgroundTaskService)
        : this(clipboard, backgroundTaskService, ProbeAsync) { }

    internal PortScannerViewModel(IClipboardService clipboard, IBackgroundTaskService backgroundTaskService,
        Func<string, int, TimeSpan, CancellationToken, Task<bool>> probe)
    {
        _probe = probe;
        _clipboard = clipboard;
        _backgroundTaskService = backgroundTaskService;

        ScanCommand          = new AsyncRelayCommand(_ => RunScanAsync(), _ => !IsScanning);
        StopCommand          = new RelayCommand(_ => StopScan(),           _ => IsScanning);
        CopyOpenPortsCommand = new RelayCommand(_ => CopyOpenPorts(),     _ => Results.Count > 0);
    }

    /// <summary>
    /// Parses a port range string like "22,80-90,443" into a list of port numbers.
    /// </summary>
    internal static List<int> ParsePorts(string input)
    {
        var ports = new HashSet<int>();
        foreach (var segment in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = segment.Trim();
            if (trimmed.Contains('-'))
            {
                var parts = trimmed.Split('-');
                if (parts.Length == 2
                    && int.TryParse(parts[0].Trim(), out var start)
                    && int.TryParse(parts[1].Trim(), out var end))
                {
                    for (var p = Math.Max(1, start); p <= Math.Min(65535, end); p++)
                        ports.Add(p);
                }
            }
            else if (int.TryParse(trimmed, out var port) && port >= 1 && port <= 65535)
            {
                ports.Add(port);
            }
        }
        return [.. ports.OrderBy(x => x)];
    }

    internal async Task RunScanAsync()
    {
        if (IsScanning) return;
        var host = Host.Trim();
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
        { StatusMessage = "Enter a hostname or IP address without a URL path."; return; }
        var ports = ParsePorts(PortRangeText);
        if (ports.Count == 0) { StatusMessage = "No valid ports in range."; return; }
        var taskId = _backgroundTaskService.BeginTask("Port scan");
        _scanTaskId = taskId;
        IsScanning = true;
        Results.Clear();
        Progress = 0;
        var ct = _backgroundTaskService.GetCancellationToken(taskId);
        var scanned = 0;
        var openCount = 0;
        var timeout = TimeSpan.FromMilliseconds(TimeoutMs);
        StatusMessage = $"Scanning {ports.Count} port(s) on {host}…";
        try
        {
            await Parallel.ForEachAsync(ports, new ParallelOptions
            {
                MaxDegreeOfParallelism = Concurrency, CancellationToken = ct
            }, async (port, token) =>
            {
                var sw = Stopwatch.StartNew();
                var open = await _probe(host, port, timeout, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (open) Interlocked.Increment(ref openCount);
                var done = Interlocked.Increment(ref scanned);
                void Update()
                {
                    if (open) Results.Add(new PortResult
                    {
                        Port = port, IsOpen = true, Status = "Open", ResponseMs = sw.ElapsedMilliseconds,
                        Service = WellKnownServices.GetValueOrDefault(port, string.Empty)
                    });
                    Progress = Math.Max(Progress, done * 100.0 / ports.Count);
                    StatusMessage = $"{openCount} open port(s) found, {scanned} scanned of {ports.Count}";
                }
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is not null) await dispatcher.InvokeAsync(Update);
                else lock (Results) Update();
                _backgroundTaskService.ReportProgress(taskId, new BackgroundTaskProgress
                { Percent = (int)(done * 100.0 / ports.Count), Message = "Scanning ports", Detail = $"{done}/{ports.Count}" });
            });
            StatusMessage = $"Scan complete. {openCount} open port(s) found of {ports.Count} scanned.";
            _backgroundTaskService.CompleteTask(taskId, "Port scan completed.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            StatusMessage = $"Scan stopped. {openCount} open port(s) found; {scanned}/{ports.Count} scanned.";
            _backgroundTaskService.CancelTask(taskId, "Port scan canceled.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
            _backgroundTaskService.FailTask(taskId, ex);
        }
        finally { IsScanning = false; _scanTaskId = null; }
    }

    public void Dispose() => StopScan();
    private static async Task<bool> ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var tcp = new TcpClient();
        try { await tcp.ConnectAsync(host, port, deadline.Token).ConfigureAwait(false); return true; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
        catch (SocketException) { return false; }
    }

    private void StopScan()
    {
        if (_scanTaskId is Guid taskId)
        {
            _backgroundTaskService.CancelTask(taskId, "Cancellation requested by user.");
        }
    }

    private void CopyOpenPorts()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Open ports on {Host}:");
        foreach (var r in Results.Where(r => r.IsOpen))
            sb.AppendLine($"  Port {r.Port,-6} {r.Service,-14} {r.ResponseMs} ms");
        _clipboard.SetText(sb.ToString());
        StatusMessage = "Open ports copied to clipboard.";
    }
}
