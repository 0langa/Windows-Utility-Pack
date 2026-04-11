using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using WindowsUtilityPack.Commands;
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
public class PortScannerViewModel : ViewModelBase
{
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
    private CancellationTokenSource?   _cts;

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
        set => SetProperty(ref _timeoutMs, Math.Max(50, value));
    }

    public int Concurrency
    {
        get => _concurrency;
        set => SetProperty(ref _concurrency, Math.Clamp(value, 1, 500));
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

    public PortScannerViewModel(IClipboardService clipboard)
    {
        _clipboard = clipboard;

        ScanCommand          = new AsyncRelayCommand(_ => RunScanAsync(), _ => !IsScanning);
        StopCommand          = new RelayCommand(_ => _cts?.Cancel(),      _ => IsScanning);
        CopyOpenPortsCommand = new RelayCommand(_ => CopyOpenPorts(),     _ => Results.Count > 0);
    }

    /// <summary>
    /// Parses a port range string like "22,80-90,443" into a list of port numbers.
    /// </summary>
    private static List<int> ParsePorts(string input)
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

    private async Task RunScanAsync()
    {
        if (string.IsNullOrWhiteSpace(Host)) return;

        var ports = ParsePorts(PortRangeText);
        if (ports.Count == 0)
        {
            StatusMessage = "No valid ports in range.";
            return;
        }

        _cts = new CancellationTokenSource();
        IsScanning = true;
        Results.Clear();
        Progress = 0;
        StatusMessage = $"Scanning {ports.Count} port(s) on {Host}…";

        var ct          = _cts.Token;
        var sem         = new SemaphoreSlim(Concurrency);
        var scanned     = 0;
        var openCount   = 0;
        var total       = ports.Count;
        var timeout     = TimeoutMs;
        var host        = Host;
        var tasks       = new List<Task>(total);

        foreach (var port in ports)
        {
            if (ct.IsCancellationRequested) break;

            await sem.WaitAsync(ct).ConfigureAwait(false);

            var capturedPort = port;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var sw     = Stopwatch.StartNew();
                    var isOpen = false;

                    try
                    {
                        using var tcp = new TcpClient();
                        var connectTask = tcp.ConnectAsync(host, capturedPort);
                        var won         = await Task.WhenAny(connectTask, Task.Delay(timeout, ct));
                        isOpen = won == connectTask && connectTask.IsCompletedSuccessfully;
                    }
                    catch { /* closed / error */ }

                    sw.Stop();

                    var result = new PortResult
                    {
                        Port       = capturedPort,
                        IsOpen     = isOpen,
                        Status     = isOpen ? "Open" : "Closed",
                        Service    = WellKnownServices.TryGetValue(capturedPort, out var svc) ? svc : string.Empty,
                        ResponseMs = sw.ElapsedMilliseconds,
                    };

                    if (isOpen)
                    {
                        Interlocked.Increment(ref openCount);
                        Application.Current.Dispatcher.Invoke(() => Results.Add(result));
                    }

                    var done = Interlocked.Increment(ref scanned);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Progress      = done * 100.0 / total;
                        StatusMessage = $"{openCount} open port(s) found, {done} scanned of {total}";
                    });
                }
                finally
                {
                    sem.Release();
                }
            }, ct));
        }

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { /* user stopped */ }

        IsScanning    = false;
        Progress      = 100;
        var cancelled = ct.IsCancellationRequested;
        StatusMessage = cancelled
            ? $"Scan stopped. {openCount} open port(s) found."
            : $"Scan complete. {openCount} open port(s) found of {total} scanned.";
        _cts?.Dispose();
        _cts = null;
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
