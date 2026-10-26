using System.Collections.ObjectModel;
using System.Text;
using DnsClient;
using DnsClient.Protocol;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.DnsLookup;

/// <summary>Represents a single DNS record result.</summary>
public class DnsRecord
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Ttl { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for DNS lookup (A, AAAA, CNAME, MX, TXT).
/// </summary>
public class DnsLookupViewModel : ViewModelBase, IDisposable
{
    private readonly IClipboardService _clipboard;
    private readonly LookupClient _lookupClient;
    private CancellationTokenSource? _lookupCancellation;
    public RelayCommand CancelCommand { get; }
    public void Dispose() => _lookupCancellation?.Cancel();

    private string _host = "example.com";
    private bool _isLooking;
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

    public AsyncRelayCommand LookupCommand { get; }
    public RelayCommand CopyResultsCommand { get; }
    public RelayCommand ClearCommand { get; }

    public DnsLookupViewModel(IClipboardService clipboard)
    {
        _clipboard = clipboard;
        _lookupClient = new LookupClient(new LookupClientOptions
        {
            UseCache = false,
            Timeout = TimeSpan.FromSeconds(5),
            Retries = 1,
        });

        LookupCommand = new AsyncRelayCommand(_ => RunLookupAsync(), _ => !IsLooking);
        CopyResultsCommand = new RelayCommand(_ => CopyResults(), _ => Results.Count > 0);
        ClearCommand = new RelayCommand(_ => Clear(), _ => !IsLooking);
        CancelCommand = new RelayCommand(() => _lookupCancellation?.Cancel(), () => IsLooking);
    }

    private async Task RunLookupAsync()
    {
        var host = NormalizeHost(Host);
        if (string.IsNullOrWhiteSpace(host))
        {
            StatusMessage = "Please enter a valid hostname.";
            return;
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        _lookupCancellation = cancellation;
        IsLooking = true;
        Results.Clear();
        StatusMessage = $"Looking up {host}...";

        try
        {
            var types = new[] { QueryType.A, QueryType.AAAA, QueryType.CNAME, QueryType.MX, QueryType.TXT };
            var responses = await Task.WhenAll(types.Select(type => _lookupClient.QueryAsync(host, type, QueryClass.IN, cancellation.Token)));
            foreach (var response in responses) AddResponse(response);
            StatusMessage = Results.Count > 0
                ? $"Found {Results.Count} record(s) for {host}."
                : $"No records found for {host}.";
        }
        catch (OperationCanceledException) { StatusMessage = "Lookup canceled or timed out."; }
        catch (DnsResponseException ex)
        {
            StatusMessage = $"DNS query failed ({ex.Code}): {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLooking = false;
            _lookupCancellation = null;
        }
    }

    private void AddResponse(IDnsQueryResponse response)
    {
        foreach (var answer in response.Answers)
        {
            switch (answer)
            {
                case ARecord a:
                    AddRecord("A", a.Address.ToString(), a.InitialTimeToLive);
                    break;
                case AaaaRecord aaaa:
                    AddRecord("AAAA", aaaa.Address.ToString(), aaaa.InitialTimeToLive);
                    break;
                case CNameRecord cname:
                    AddRecord("CNAME", cname.CanonicalName.Value, cname.InitialTimeToLive);
                    break;
                case MxRecord mx:
                    AddRecord("MX", $"{mx.Preference} {mx.Exchange.Value}", mx.InitialTimeToLive);
                    break;
                case TxtRecord txt:
                    AddRecord("TXT", string.Join(" ", txt.Text), txt.InitialTimeToLive);
                    break;
            }
        }
    }

    private void AddRecord(string type, string value, int ttl)
    {
        if (Results.Any(r => r.Type == type && r.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
            return;

        Results.Add(new DnsRecord
        {
            Type = type,
            Value = value,
            Ttl = $"{ttl}s",
        });
    }

    private void CopyResults()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DNS Lookup: {NormalizeHost(Host)}");
        sb.AppendLine(new string('-', 50));
        foreach (var r in Results)
            sb.AppendLine($"{r.Type,-8} {r.Value}  (TTL {r.Ttl})");
        _clipboard.SetText(sb.ToString());
        StatusMessage = "Results copied to clipboard.";
    }

    private void Clear()
    {
        Results.Clear();
        StatusMessage = string.Empty;
        Host = "example.com";
    }

    private static string NormalizeHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return string.Empty;

        host = host.Trim();
        if (Uri.TryCreate(host, UriKind.Absolute, out var absolute) && !string.IsNullOrWhiteSpace(absolute.Host))
            return absolute.Host;

        return host.Split('/')[0];
    }
}
