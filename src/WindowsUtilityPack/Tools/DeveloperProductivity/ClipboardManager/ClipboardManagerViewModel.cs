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
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _pollTimer;

    private ClipboardHistoryEntry? _selectedEntry;
    private string _statusMessage = "Ready";
    private bool _isMonitoring;
    private bool _captureSensitiveContent;
    private int _retentionDays = 30;
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
                if (value && !EnsureMonitoringConsent())
                {
                    _isMonitoring = false;
                    OnPropertyChanged(nameof(IsMonitoring));
                    return;
                }

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

                PersistPrivacySettings();
            }
        }
    }

    public bool CaptureSensitiveContent
    {
        get => _captureSensitiveContent;
        set
        {
            if (SetProperty(ref _captureSensitiveContent, value))
            {
                PersistPrivacySettings();
            }
        }
    }

    public int RetentionDays
    {
        get => _retentionDays;
        set
        {
            var normalized = Math.Clamp(value, 0, 3650);
            if (SetProperty(ref _retentionDays, normalized))
            {
                PersistPrivacySettings();
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
        IUserDialogService dialogService,
        ISettingsService settingsService)
    {
        _clipboardService = clipboardService;
        _historyService = historyService;
        _dialogService = dialogService;
        _settingsService = settingsService;

        var settings = _settingsService.Load();
        _isMonitoring = settings.ClipboardMonitoringEnabled && settings.ClipboardMonitoringConsentAccepted;
        _captureSensitiveContent = settings.ClipboardCaptureSensitiveContent;
        _retentionDays = Math.Clamp(settings.ClipboardHistoryRetentionDays, 0, 3650);

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        CopySelectedCommand = new RelayCommand(_ => CopySelected(), _ => SelectedEntry is not null);
        DeleteSelectedCommand = new AsyncRelayCommand(_ => DeleteSelectedAsync(), _ => SelectedEntry is not null);
        ClearAllCommand = new AsyncRelayCommand(_ => ClearAllAsync(), _ => Entries.Count > 0);

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1.5),
        };
        _pollTimer.Tick += OnPollTimerTick;
        if (_isMonitoring)
        {
            _pollTimer.Start();
        }

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
        else
        {
            StatusMessage = "Clipboard entry skipped by privacy policy or duplicate filtering.";
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
        PersistPrivacySettings();
        _pollTimer.Tick -= OnPollTimerTick;
        _pollTimer.Stop();
    }

    private bool EnsureMonitoringConsent()
    {
        var settings = _settingsService.Load();
        if (settings.ClipboardMonitoringConsentAccepted)
        {
            return true;
        }

        var accepted = _dialogService.Confirm(
            "Enable clipboard monitoring",
            "Clipboard monitoring can capture sensitive content copied from other applications. Enable monitoring?");
        if (!accepted)
        {
            return false;
        }

        settings.ClipboardMonitoringConsentAccepted = true;
        _settingsService.Save(settings);
        return true;
    }

    private void PersistPrivacySettings()
    {
        try
        {
            var settings = _settingsService.Load();
            settings.ClipboardMonitoringEnabled = _isMonitoring;
            settings.ClipboardCaptureSensitiveContent = _captureSensitiveContent;
            settings.ClipboardHistoryRetentionDays = Math.Clamp(_retentionDays, 0, 3650);
            _settingsService.Save(settings);
        }
        catch (Exception ex)
        {
            App.TryGetLoggingService()?.LogError("Failed to persist clipboard privacy settings.", ex);
        }
    }
}
