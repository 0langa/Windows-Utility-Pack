using System.Collections.ObjectModel;
using System.Text;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.EventLogViewer;

/// <summary>
/// ViewModel for event log filtering and diagnostics review.
/// </summary>
public sealed class EventLogViewerViewModel : ViewModelBase
{
    private readonly IWindowsEventLogService _eventLogService;
    private readonly IClipboardService _clipboard;

    private string _selectedLogName = "Application";
    private string _sourceFilter = string.Empty;
    private string _levelFilter = string.Empty;
    private string _eventIdFilterText = string.Empty;
    private int _limit = 200;
    private string _statusMessage = "Configure filters and click Refresh.";

    public ObservableCollection<WindowsEventLogRecord> Entries { get; } = [];

    public IReadOnlyList<string> LogNames { get; } = ["Application", "System", "Security"];

    public string SelectedLogName
    {
        get => _selectedLogName;
        set => SetProperty(ref _selectedLogName, value);
    }

    public string SourceFilter
    {
        get => _sourceFilter;
        set => SetProperty(ref _sourceFilter, value);
    }

    public string LevelFilter
    {
        get => _levelFilter;
        set => SetProperty(ref _levelFilter, value);
    }

    public string EventIdFilterText
    {
        get => _eventIdFilterText;
        set => SetProperty(ref _eventIdFilterText, value);
    }

    public int Limit
    {
        get => _limit;
        set => SetProperty(ref _limit, Math.Clamp(value, 10, 2000));
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand CopyCsvCommand { get; }

    public EventLogViewerViewModel(IWindowsEventLogService eventLogService, IClipboardService clipboard)
    {
        _eventLogService = eventLogService ?? throw new ArgumentNullException(nameof(eventLogService));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        CopyCsvCommand = new RelayCommand(_ => CopyCsv(), _ => Entries.Count > 0);
    }

    internal async Task RefreshAsync()
    {
        var eventIdFilter = int.TryParse(EventIdFilterText, out var parsedId) ? parsedId : (int?)null;
        var entries = await _eventLogService.QueryAsync(
            SelectedLogName,
            SourceFilter,
            LevelFilter,
            eventIdFilter,
            DateTime.UtcNow.AddDays(-7),
            Limit).ConfigureAwait(true);

        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(entry);
        }

        StatusMessage = Entries.Count == 0
            ? "No events found for current filter."
            : $"Loaded {Entries.Count:N0} events from {SelectedLogName}.";
    }

    private void CopyCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("TimestampUtc,LogName,Source,EventId,Level,Message");

        foreach (var entry in Entries)
        {
            sb.Append(E(entry.TimestampUtc.ToString("O"))).Append(',')
              .Append(E(entry.LogName)).Append(',')
              .Append(E(entry.Source)).Append(',')
              .Append(entry.EventId).Append(',')
              .Append(E(entry.Level)).Append(',')
              .Append(E(entry.Message))
              .AppendLine();
        }

        _clipboard.SetText(sb.ToString());
        StatusMessage = "Copied event records as CSV.";
    }

    private static string E(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var escaped = value.Replace("\"", "\"\"");
        if (escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r'))
            return $"\"{escaped}\"";
        return escaped;
    }
}