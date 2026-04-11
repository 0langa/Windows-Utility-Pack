using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
public class PortScannerViewModel : ViewModelBase
{
    internal const int MaxSafeConcurrency = 100;

    private static readonly Dictionary<int, string> WellKnownServices = new()
    {
        { 20,    "FTP-Data"       },
        { 21,    "FTP"            },
        { 22,    "SSH"            },
        { 23,    "Telnet"         },
        { 25,    "SMTP"           },
        { 53,    "DNS"            },
        { 67,    "DHCP-Server"    },
        { 68,    "DHCP-Client"    },
        { 69,    "TFTP"           },
        { 80,    "HTTP"           },
        { 88,    "Kerberos"       },
        { 110,   "POP3"           },
        { 111,   "RPC"            },
        { 119,   "NNTP"           },
        { 123,   "NTP"            },
        { 135,   "MS-RPC"         },
        { 137,   "NetBIOS-NS"     },
        { 138,   "NetBIOS-DGM"    },
        { 139,   "NetBIOS-SSN"    },
        { 143,   "IMAP"           },
        { 161,   "SNMP"           },
        { 162,   "SNMP-Trap"      },
        { 179,   "BGP"            },
        { 389,   "LDAP"           },
        { 443,   "HTTPS"          },
        { 445,   "SMB"            },
        { 465,   "SMTPS"          },
        { 500,   "IKE/IPsec"      },
        { 514,   "Syslog"         },
        { 515,   "LPD/LPR"        },
        { 587,   "SMTP-Submission" },
        { 636,   "LDAPS"          },
        { 993,   "IMAPS"          },
        { 995,   "POP3S"          },
        { 1080,  "SOCKS"          },
        { 1433,  "MSSQL"          },
        { 1521,  "Oracle"         },
        { 1723,  "PPTP"           },
        { 2181,  "ZooKeeper"      },
        { 2375,  "Docker"         },
        { 2376,  "Docker-TLS"     },
        { 3000,  "Dev-HTTP"       },
        { 3268,  "LDAP-GC"        },
        { 3306,  "MySQL"          },
        { 3389,  "RDP"            },
        { 4369,  "RabbitMQ-EPMD"  },
        { 5000,  "Dev-HTTP"       },
        { 5432,  "PostgreSQL"     },
        { 5601,  "Kibana"         },
        { 5672,  "AMQP"           },
        { 5900,  "VNC"            },
        { 5985,  "WinRM-HTTP"     },
        { 5986,  "WinRM-HTTPS"    },
        { 6379,  "Redis"          },
        { 6443,  "Kubernetes-API" },
        { 7001,  "WebLogic"       },
        { 8080,  "HTTP-Alt"       },
        { 8443,  "HTTPS-Alt"      },
        { 8888,  "Dev-HTTP"       },
        { 9000,  "SonarQube"      },
        { 9200,  "Elasticsearch"  },
        { 9300,  "Elasticsearch-C"},
        { 9418,  "Git"            },
        { 11211, "Memcached"      },
        { 15672, "RabbitMQ-Mgmt"  },
        { 27017, "MongoDB"        },
        { 27018, "MongoDB-Shard"  },
        { 50000, "SAP"            },
        { 50070, "Hadoop-HDFS"    },
    };

    private readonly IClipboardService _clipboard;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private Guid? _scanTaskId;

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
    public RelayCommand      ExportCsvCommand     { get; }

    public PortScannerViewModel(IClipboardService clipboard, IBackgroundTaskService backgroundTaskService)
    {
        _clipboard = clipboard;
        _backgroundTaskService = backgroundTaskService;

        ScanCommand          = new AsyncRelayCommand(_ => RunScanAsync(), _ => !IsScanning);
        StopCommand          = new RelayCommand(_ => StopScan(),           _ => IsScanning);
        CopyOpenPortsCommand = new RelayCommand(_ => CopyOpenPorts(),     _ => Results.Count > 0);
        ExportCsvCommand     = new RelayCommand(_ => ExportCsv(),         _ => Results.Count > 0);
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

    private async Task RunScanAsync()
    {
        if (string.IsNullOrWhiteSpace(Host)) return;

        var ports = ParsePorts(PortRangeText);
        if (ports.Count == 0)
        {
            StatusMessage = "No valid ports in range.";
            return;
        }

        if (_scanTaskId is Guid previousTask)
        {
            _backgroundTaskService.CancelTask(previousTask, "Superseded by a new scan request.");
        }

        var taskId = _backgroundTaskService.BeginTask("Port scan");
        _scanTaskId = taskId;

        IsScanning = true;
        Results.Clear();
        Progress = 0;
        StatusMessage = $"Scanning {ports.Count} port(s) on {Host}…";
        _backgroundTaskService.ReportProgress(taskId, new BackgroundTaskProgress
        {
            Percent = 0,
            Message = "Port scan started",
            Detail = Host,
        });

        var ct          = _backgroundTaskService.GetCancellationToken(taskId);
        var sem         = new SemaphoreSlim(Math.Clamp(Concurrency, 1, MaxSafeConcurrency));
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

                    _backgroundTaskService.ReportProgress(taskId, new BackgroundTaskProgress
                    {
                        Percent = (int)Math.Round(done * 100.0 / total),
                        Message = "Scanning ports",
                        Detail = $"{done}/{total} scanned",
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

        if (cancelled)
        {
            _backgroundTaskService.CancelTask(taskId, "Port scan cancelled.");
        }
        else
        {
            _backgroundTaskService.CompleteTask(taskId, "Port scan completed.");
        }

        _scanTaskId = null;
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

    private void ExportCsv()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title       = "Export scan results",
            FileName    = $"portscan_{Host}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            DefaultExt  = ".csv",
            Filter      = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Port,Service,Status,ResponseMs");
            foreach (var r in Results)
                sb.AppendLine($"{r.Port},{EscapeCsv(r.Service)},{EscapeCsv(r.Status)},{r.ResponseMs}");
            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"Results exported to {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
