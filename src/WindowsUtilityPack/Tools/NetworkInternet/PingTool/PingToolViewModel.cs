using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Threading;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.PingTool;

/// <summary>Represents the result of a single ping attempt.</summary>
public class PingResultItem
{
    public int    Attempt     { get; init; }
    public string Host        { get; init; } = string.Empty;
    public string Status      { get; init; } = string.Empty;
    public long   RoundtripMs { get; init; }
    public string Address     { get; init; } = string.Empty;

    /// <summary>True when the ping replied with <see cref="IPStatus.Success"/>.</summary>
    public bool   IsSuccess   { get; init; }
}

/// <summary>
/// ViewModel for the Ping Tool.
/// Sends a configurable number of ICMP echo requests to the target host
/// and collects per-attempt results and a summary.
///
/// Key design notes:
/// <list type="bullet">
///   <item>Uses <see cref="AsyncRelayCommand"/> so the Ping button is disabled during execution.</item>
///   <item>A 500 ms gap between attempts mimics the behaviour of the system ping command.</item>
///   <item>Each attempt is individually try/catched so one failure does not abort the sequence.</item>
///   <item>The <see cref="StopCommand"/> cancels the running sequence via <see cref="CancellationTokenSource"/>.</item>
///   <item>Summary includes min/max/avg latency and packet loss percentage.</item>
/// </list>
/// </summary>
public class PingToolViewModel : ViewModelBase
{
    private CancellationTokenSource? _cts;

    private string _host        = "google.com";
    private int    _pingCount   = 4;
    private bool   _isPinging;
    private string _summary     = string.Empty;

    /// <summary>Hostname or IP address to ping.</summary>
    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    /// <summary>Number of ping attempts to send (clamped to 1–20).</summary>
    public int PingCount
    {
        get => _pingCount;
        set => SetProperty(ref _pingCount, Math.Clamp(value, 1, 20));
    }

    /// <summary>True while pinging is in progress (used to show "Pinging…" indicator).</summary>
    public bool IsPinging
    {
        get => _isPinging;
        set => SetProperty(ref _isPinging, value);
    }

    /// <summary>Summary line shown below the results table.</summary>
    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    /// <summary>Per-attempt ping results displayed in the results table.</summary>
    public ObservableCollection<PingResultItem> Results { get; } = [];

    /// <summary>Starts pinging the target host.  Disabled while a ping sequence is running.</summary>
    public AsyncRelayCommand PingCommand { get; }

    /// <summary>Cancels the current ping sequence.  Enabled only while pinging.</summary>
    public RelayCommand StopCommand { get; }

    public PingToolViewModel()
    {
        PingCommand = new AsyncRelayCommand(_ => RunPingAsync(), _ => !IsPinging);
        StopCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsPinging);
    }

    private async Task RunPingAsync()
    {
        if (string.IsNullOrWhiteSpace(Host)) return;

        _cts = new CancellationTokenSource();
        IsPinging = true;
        Results.Clear();
        Summary = string.Empty;

        try
        {
            using var ping = new Ping();
            var successful = 0;
            long totalMs   = 0;
            long minMs     = long.MaxValue;
            long maxMs     = 0;

            for (var i = 1; i <= PingCount; i++)
            {
                if (_cts?.Token.IsCancellationRequested == true) break;

                try
                {
                    var reply = await ping.SendPingAsync(Host, 3000);
                    var isOk  = reply.Status == IPStatus.Success;
                    if (isOk)
                    {
                        successful++;
                        totalMs += reply.RoundtripTime;
                        if (reply.RoundtripTime < minMs) minMs = reply.RoundtripTime;
                        if (reply.RoundtripTime > maxMs) maxMs = reply.RoundtripTime;
                    }

                    Results.Add(new PingResultItem
                    {
                        Attempt     = i,
                        Host        = Host,
                        Status      = reply.Status.ToString(),
                        RoundtripMs = reply.RoundtripTime,
                        Address     = reply.Address?.ToString() ?? "-",
                        IsSuccess   = isOk,
                    });
                }
                catch (Exception ex)
                {
                    Results.Add(new PingResultItem
                    {
                        Attempt = i, Host = Host, Status = ex.Message, IsSuccess = false,
                    });
                }

                // Space out attempts to mimic standard ping behaviour.
                if (i < PingCount && _cts?.Token.IsCancellationRequested != true)
                {
                    try { await Task.Delay(500, _cts!.Token); }
                    catch (TaskCanceledException) { break; }
                }
            }

            var sent    = Results.Count;
            var lossPercent = sent > 0 ? (int)((sent - successful) * 100.0 / sent) : 0;
            var avgMs   = successful > 0 ? totalMs / successful : 0;

            if (successful > 0)
            {
                Summary = $"{successful}/{sent} successful | " +
                          $"Min: {minMs} ms | Max: {maxMs} ms | Avg: {avgMs} ms | " +
                          $"Loss: {lossPercent}%";
            }
            else
            {
                Summary = $"0/{sent} successful | Loss: {lossPercent}%";
            }

            if (_cts?.Token.IsCancellationRequested == true)
                Summary += " (cancelled)";
        }
        finally
        {
            IsPinging = false;
            _cts = null;
        }
    }
}
