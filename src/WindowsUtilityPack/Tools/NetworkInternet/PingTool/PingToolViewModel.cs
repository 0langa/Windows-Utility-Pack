using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.PingTool;

public class PingResultItem
{
    public int Attempt { get; init; }
    public string Host { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long RoundtripMs { get; init; }
    public string Address { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
}

public class PingToolViewModel : ViewModelBase
{
    private string _host = "google.com";
    private int _pingCount = 4;
    private bool _isPinging;
    private string _summary = string.Empty;

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public int PingCount
    {
        get => _pingCount;
        set => SetProperty(ref _pingCount, Math.Clamp(value, 1, 20));
    }

    public bool IsPinging
    {
        get => _isPinging;
        set => SetProperty(ref _isPinging, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public ObservableCollection<PingResultItem> Results { get; } = [];

    public AsyncRelayCommand PingCommand { get; }

    public PingToolViewModel()
    {
        PingCommand = new AsyncRelayCommand(_ => RunPingAsync(), _ => !IsPinging);
    }

    private async Task RunPingAsync()
    {
        if (string.IsNullOrWhiteSpace(Host)) return;

        IsPinging = true;
        Results.Clear();
        Summary = string.Empty;

        try
        {
            using var ping = new Ping();
            var successful = 0;
            long totalMs = 0;

            for (var i = 1; i <= PingCount; i++)
            {
                try
                {
                    var reply = await ping.SendPingAsync(Host, 3000);
                    var isOk = reply.Status == IPStatus.Success;
                    if (isOk) { successful++; totalMs += reply.RoundtripTime; }

                    Results.Add(new PingResultItem
                    {
                        Attempt = i,
                        Host = Host,
                        Status = reply.Status.ToString(),
                        RoundtripMs = reply.RoundtripTime,
                        Address = reply.Address?.ToString() ?? "-",
                        IsSuccess = isOk,
                    });
                }
                catch (Exception ex)
                {
                    Results.Add(new PingResultItem
                    {
                        Attempt = i, Host = Host, Status = ex.Message, IsSuccess = false,
                    });
                }

                if (i < PingCount) await Task.Delay(500);
            }

            var avgMs = successful > 0 ? totalMs / successful : 0;
            Summary = $"{successful}/{PingCount} successful | Avg: {avgMs} ms";
        }
        finally
        {
            IsPinging = false;
        }
    }
}
