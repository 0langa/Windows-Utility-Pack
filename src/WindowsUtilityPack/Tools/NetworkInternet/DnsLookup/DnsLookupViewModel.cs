using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.DnsLookup;

/// <summary>Represents a single DNS record result.</summary>
public class DnsRecord
{
    public string Type  { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Ttl   { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the DNS Lookup tool.
/// Resolves a hostname using <see cref="Dns.GetHostEntryAsync"/> and surfaces
/// A/AAAA/CNAME records in an observable collection.
/// </summary>
public class DnsLookupViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboard;

    private string _host          = "example.com";
    private bool   _isLooking;
    private string _statusMessage = string.Empty;

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public bool IsLooking
    {
        get => _isLooking;
        set => SetProperty(ref _isLooking, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<DnsRecord> Results { get; } = [];

    public AsyncRelayCommand LookupCommand    { get; }
    public RelayCommand      CopyResultsCommand { get; }
    public RelayCommand      ClearCommand     { get; }

    public DnsLookupViewModel(IClipboardService clipboard)
    {
        _clipboard = clipboard;

        LookupCommand     = new AsyncRelayCommand(_ => RunLookupAsync(), _ => !IsLooking);
        CopyResultsCommand = new RelayCommand(_ => CopyResults(),        _ => Results.Count > 0);
        ClearCommand      = new RelayCommand(_ => Clear());
    }

    private async Task RunLookupAsync()
    {
        if (string.IsNullOrWhiteSpace(Host)) return;

        IsLooking = true;
        Results.Clear();
        StatusMessage = $"Looking up {Host}…";

        try
        {
            var entry = await Dns.GetHostEntryAsync(Host);

            foreach (var addr in entry.AddressList)
            {
                var type = addr.AddressFamily == AddressFamily.InterNetworkV6 ? "AAAA" : "A";
                Application.Current.Dispatcher.Invoke(() =>
                    Results.Add(new DnsRecord { Type = type, Value = addr.ToString(), Ttl = "N/A" }));
            }

            foreach (var alias in entry.Aliases)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    Results.Add(new DnsRecord { Type = "CNAME/Alias", Value = alias, Ttl = "N/A" }));
            }

            // Also try GetHostAddressesAsync for completeness
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(Host);
                foreach (var addr in addresses)
                {
                    var type = addr.AddressFamily == AddressFamily.InterNetworkV6 ? "AAAA" : "A";
                    var val  = addr.ToString();
                    // Avoid duplicates already in Results
                    if (!Results.Any(r => r.Value == val))
                        Application.Current.Dispatcher.Invoke(() =>
                            Results.Add(new DnsRecord { Type = type, Value = val, Ttl = "N/A" }));
                }
            }
            catch
            {
                // ignore secondary lookup errors
            }

            StatusMessage = Results.Count > 0
                ? $"Found {Results.Count} record(s) for {Host}"
                : $"No records found for {Host}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLooking = false;
        }
    }

    private void CopyResults()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DNS Lookup: {Host}");
        sb.AppendLine(new string('-', 50));
        foreach (var r in Results)
            sb.AppendLine($"{r.Type,-15} {r.Value}");
        _clipboard.SetText(sb.ToString());
        StatusMessage = "Results copied to clipboard.";
    }

    private void Clear()
    {
        Results.Clear();
        StatusMessage = string.Empty;
        Host = "example.com";
    }
}
