using System.Collections.ObjectModel;
using System.Windows.Threading;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.ClipboardManager;

/// <summary>
/// ViewModel for the Clipboard Manager tool.
/// </summary>
public sealed class ClipboardManagerViewModel : ViewModelBase, IDisposable
{
    private readonly IClipboardService _clipboardService;
    private readonly IClipboardHistoryService _historyService;
    private readonly IUserDialogService _dialogService;
    private readonly DispatcherTimer _pollTimer;

    private ClipboardHistoryEntry? _selectedEntry;
    private string _statusMessage = "Ready";
    private bool _isMonitoring = true;
    private string _searchText = string.Empty;
    private string _lastObservedText = string.Empty;

    public ObservableCollection<ClipboardHistoryEntry> Entries { get; } = [];

    public ClipboardHistoryEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsMonitoring
    {
        get => _isMonitoring;
        set
        {
            if (SetProperty(ref _isMonitoring, value))
            {
                if (_isMonitoring)
                {
                    _pollTimer.Start();
                    StatusMessage = "Clipboard monitoring enabled.";
                }
                else
                {
                    _pollTimer.Stop();
                    StatusMessage = "Clipboard monitoring paused.";
                }
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand CopySelectedCommand { get; }
    public AsyncRelayCommand DeleteSelectedCommand { get; }
    public AsyncRelayCommand ClearAllCommand { get; }

    public ClipboardManagerViewModel(
        IClipboardService clipboardService,
        IClipboardHistoryService historyService,
        IUserDialogService dialogService)
    {
        _clipboardService = clipboardService;
        _historyService = historyService;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        CopySelectedCommand = new RelayCommand(_ => CopySelected(), _ => SelectedEntry is not null);
        DeleteSelectedCommand = new AsyncRelayCommand(_ => DeleteSelectedAsync(), _ => SelectedEntry is not null);
        ClearAllCommand = new AsyncRelayCommand(_ => ClearAllAsync(), _ => Entries.Count > 0);

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1.5),
        };
        _pollTimer.Tick += OnPollTimerTick;
        _pollTimer.Start();

        _ = RefreshAsync();
    }

    private async void OnPollTimerTick(object? sender, EventArgs e)
    {
        if (!_clipboardService.TryGetText(out var currentText))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentText))
        {
            return;
        }

        if (string.Equals(currentText, _lastObservedText, StringComparison.Ordinal))
        {
            return;
        }

        _lastObservedText = currentText;
        var inserted = await _historyService.AddEntryAsync(currentText).ConfigureAwait(true);
        if (inserted > 0)
        {
            await RefreshAsync().ConfigureAwait(true);
            StatusMessage = "Captured clipboard entry.";
        }
    }

    private async Task RefreshAsync()
    {
        var entries = await _historyService.GetRecentAsync(300).ConfigureAwait(true);

        Entries.Clear();
        IEnumerable<ClipboardHistoryEntry> filtered = entries;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(e => e.Content.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var entry in filtered)
        {
            Entries.Add(entry);
        }

        StatusMessage = Entries.Count == 0
            ? "No clipboard history entries yet."
            : $"Loaded {Entries.Count:N0} clipboard entries.";
    }

    private void CopySelected()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        _clipboardService.SetText(SelectedEntry.Content);
        StatusMessage = "Selected entry copied to clipboard.";
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        var removed = await _historyService.DeleteEntryAsync(SelectedEntry.Id).ConfigureAwait(true);
        if (removed)
        {
            StatusMessage = "Deleted selected entry.";
            await RefreshAsync().ConfigureAwait(true);
        }
    }

    private async Task ClearAllAsync()
    {
        var confirmed = _dialogService.Confirm(
            "Clear clipboard history",
            "This clears all saved clipboard entries. Continue?");

        if (!confirmed)
        {
            return;
        }

        await _historyService.ClearAsync().ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        StatusMessage = "Clipboard history cleared.";
    }

    public void Dispose()
    {
        _pollTimer.Tick -= OnPollTimerTick;
        _pollTimer.Stop();
    }
}