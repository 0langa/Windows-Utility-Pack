using System.Collections.ObjectModel;
using System.Text;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.ActivityLog;

/// <summary>
/// ViewModel for reviewing and exporting application activity events.
/// </summary>
public sealed class ActivityLogViewModel : ViewModelBase
{
    private readonly IActivityLogService _activity;
    private readonly IClipboardService _clipboard;
    private readonly IUserDialogService _dialogs;

    private string _categoryFilter = string.Empty;
    private int _limit = 300;
    private string _statusMessage = "Load recent activity events.";

    public ObservableCollection<ActivityLogEntry> Entries { get; } = [];

    public string CategoryFilter
    {
        get => _categoryFilter;
        set => SetProperty(ref _categoryFilter, value);
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
    public AsyncRelayCommand ClearCommand { get; }
    public RelayCommand CopyCsvCommand { get; }

    public ActivityLogViewModel(IActivityLogService activity, IClipboardService clipboard, IUserDialogService dialogs)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        ClearCommand = new AsyncRelayCommand(_ => ClearAsync());
        CopyCsvCommand = new RelayCommand(_ => CopyCsv(), _ => Entries.Count > 0);

        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var logs = await _activity.GetRecentAsync(
            Limit,
            string.IsNullOrWhiteSpace(CategoryFilter) ? null : CategoryFilter.Trim())
            .ConfigureAwait(true);

        Entries.Clear();
        foreach (var entry in logs)
        {
            Entries.Add(entry);
        }

        StatusMessage = Entries.Count == 0
            ? "No activity entries match current filter."
            : $"Loaded {Entries.Count:N0} activity entries.";
    }

    private async Task ClearAsync()
    {
        var scope = string.IsNullOrWhiteSpace(CategoryFilter)
            ? "all categories"
            : $"category '{CategoryFilter.Trim()}'";

        if (!_dialogs.Confirm("Clear activity log", $"Delete activity log entries for {scope}?"))
        {
            return;
        }

        var deleted = await _activity.ClearAsync(
            string.IsNullOrWhiteSpace(CategoryFilter) ? null : CategoryFilter.Trim())
            .ConfigureAwait(true);

        await RefreshAsync().ConfigureAwait(true);
        StatusMessage = $"Deleted {deleted:N0} activity entries.";
    }

    private void CopyCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,TimestampUtc,Category,Action,Details,IsSensitive");

        foreach (var entry in Entries)
        {
            sb
                .Append(entry.Id).Append(',')
                .Append(Escape(entry.TimestampUtc.ToString("O"))).Append(',')
                .Append(Escape(entry.Category)).Append(',')
                .Append(Escape(entry.Action)).Append(',')
                .Append(Escape(entry.Details)).Append(',')
                .Append(entry.IsSensitive ? "true" : "false")
                .AppendLine();
        }

        _clipboard.SetText(sb.ToString());
        StatusMessage = "Copied activity entries as CSV.";
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        if (escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r'))
        {
            return $"\"{escaped}\"";
        }

        return escaped;
    }
}