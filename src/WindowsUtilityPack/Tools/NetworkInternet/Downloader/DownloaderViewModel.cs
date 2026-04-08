using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader;

/// <summary>Downloader workspace ViewModel for queue management and asset discovery.</summary>
public sealed class DownloaderViewModel : ViewModelBase
{
    private readonly IDownloadCoordinatorService _coordinator;
    private readonly IAssetDiscoveryService _assetDiscovery;
    private readonly IDownloaderSettingsService _settingsService;
    private readonly IDependencyManagerService _dependencyManager;
    private readonly IDownloadEventLogService _eventLog;
    private readonly IDownloadSchedulerService _scheduler;
    private readonly IDownloaderFileDialogService _fileDialog;
    private readonly IClipboardService _clipboard;
    private readonly IUserDialogService _dialogs;
    private readonly DispatcherTimer _clipboardTimer;

    private string _quickInput = string.Empty;
    private string _scanUrl = string.Empty;
    private string _statusMessage = "Ready";
    private string _scanStatus = string.Empty;
    private string _assetSearchText = string.Empty;
    private AssetFilterType _selectedAssetFilter = AssetFilterType.All;
    private DownloaderMode _selectedMode = DownloaderMode.QuickDownload;
    private DownloadJob? _selectedJob;
    private bool _isScanning;
    private bool _isInstallingTools;
    private DateTime _scheduledStartDate = DateTime.Today;
    private DateTime _scheduledPauseDate = DateTime.Today;
    private string _scheduledStartTimeText = "23:00";
    private string _scheduledPauseTimeText = "23:30";
    private string _schedulerStatus = "No active schedule";
    private string _lastClipboardText = string.Empty;

    public DownloaderSettings Settings { get; }

    public ObservableCollection<DownloadJob> Jobs => _coordinator.Jobs;

    public ObservableCollection<DownloadPackage> Packages => _coordinator.Packages;

    public ObservableCollection<DownloadHistoryEntry> History => _coordinator.History;

    public ObservableCollection<DownloadAssetCandidate> DiscoveredAssets { get; } = [];

    public ICollectionView DiscoveredAssetsView { get; }

    public ObservableCollection<DownloadEventRecord> RecentEvents { get; } = [];

    public ObservableCollection<DownloadJob> SelectedJobs { get; } = [];

    public IReadOnlyList<DownloaderMode> Modes { get; } =
    [
        DownloaderMode.QuickDownload,
        DownloaderMode.MediaDownload,
        DownloaderMode.AssetGrabber,
        DownloaderMode.SiteCrawl,
    ];

    public IReadOnlyList<AssetFilterType> AssetFilters { get; } =
    [
        AssetFilterType.All,
        AssetFilterType.Images,
        AssetFilterType.Video,
        AssetFilterType.Audio,
        AssetFilterType.Archives,
        AssetFilterType.Documents,
        AssetFilterType.Executables,
        AssetFilterType.CodeTextData,
        AssetFilterType.Fonts,
    ];

    public string QuickInput
    {
        get => _quickInput;
        set => SetProperty(ref _quickInput, value);
    }

    public string ScanUrl
    {
        get => _scanUrl;
        set => SetProperty(ref _scanUrl, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ScanStatus
    {
        get => _scanStatus;
        set => SetProperty(ref _scanStatus, value);
    }

    public string AssetSearchText
    {
        get => _assetSearchText;
        set
        {
            if (SetProperty(ref _assetSearchText, value))
            {
                DiscoveredAssetsView.Refresh();
            }
        }
    }

    public AssetFilterType SelectedAssetFilter
    {
        get => _selectedAssetFilter;
        set
        {
            if (SetProperty(ref _selectedAssetFilter, value))
            {
                DiscoveredAssetsView.Refresh();
            }
        }
    }

    public DownloaderMode SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    public DownloadJob? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public bool IsInstallingTools
    {
        get => _isInstallingTools;
        set => SetProperty(ref _isInstallingTools, value);
    }

    public DateTime ScheduledStartDate
    {
        get => _scheduledStartDate;
        set => SetProperty(ref _scheduledStartDate, value);
    }

    public DateTime ScheduledPauseDate
    {
        get => _scheduledPauseDate;
        set => SetProperty(ref _scheduledPauseDate, value);
    }

    public string ScheduledStartTimeText
    {
        get => _scheduledStartTimeText;
        set => SetProperty(ref _scheduledStartTimeText, value);
    }

    public string ScheduledPauseTimeText
    {
        get => _scheduledPauseTimeText;
        set => SetProperty(ref _scheduledPauseTimeText, value);
    }

    public string SchedulerStatus
    {
        get => _schedulerStatus;
        set => SetProperty(ref _schedulerStatus, value);
    }

    public int QueuedCount => _coordinator.Statistics.Queued;

    public int ActiveCount => _coordinator.Statistics.Active;

    public int PausedCount => _coordinator.Statistics.Paused;

    public int CompletedCount => _coordinator.Statistics.Completed;

    public int FailedCount => _coordinator.Statistics.Failed;

    public bool IsQueueRunning => _coordinator.IsQueueRunning;

    public bool DependenciesReady => _dependencyManager.Check().AllOk;

    public AsyncRelayCommand AddToQueueCommand { get; }

    public AsyncRelayCommand DownloadNowCommand { get; }

    public AsyncRelayCommand ImportLinksCommand { get; }

    public AsyncRelayCommand PasteClipboardCommand { get; }

    public AsyncRelayCommand InstallToolsCommand { get; }

    public AsyncRelayCommand UpdateToolsCommand { get; }

    public AsyncRelayCommand StartQueueCommand { get; }

    public AsyncRelayCommand PauseQueueCommand { get; }

    public AsyncRelayCommand StopQueueCommand { get; }

    public RelayCommand RetryFailedCommand { get; }

    public RelayCommand ClearCompletedCommand { get; }

    public RelayCommand ClearFailedCommand { get; }

    public RelayCommand PauseSelectedCommand { get; }

    public RelayCommand ResumeSelectedCommand { get; }

    public RelayCommand CancelSelectedCommand { get; }

    public RelayCommand RetrySelectedCommand { get; }

    public RelayCommand RemoveSelectedCommand { get; }

    public RelayCommand OpenSourceCommand { get; }

    public RelayCommand OpenContainingFolderCommand { get; }

    public RelayCommand CopySourceUrlCommand { get; }

    public AsyncRelayCommand ScanPageCommand { get; }

    public AsyncRelayCommand CrawlSiteCommand { get; }

    public AsyncRelayCommand AddSelectedAssetsCommand { get; }

    public RelayCommand SelectAllAssetsCommand { get; }

    public RelayCommand SelectVisibleAssetsCommand { get; }

    public RelayCommand DeselectAllAssetsCommand { get; }

    public RelayCommand InvertAssetSelectionCommand { get; }

    public RelayCommand SaveSettingsCommand { get; }

    public RelayCommand PickCookieFileCommand { get; }

    public RelayCommand ScheduleStartCommand { get; }

    public RelayCommand SchedulePauseCommand { get; }

    public RelayCommand ClearScheduleCommand { get; }

    public AsyncRelayCommand ExportDiagnosticsCommand { get; }

    public RelayCommand ClearHistoryCommand { get; }

    public AsyncRelayCommand RedownloadHistoryItemCommand { get; }

    public DownloaderViewModel(
        IDownloadCoordinatorService coordinator,
        IAssetDiscoveryService assetDiscovery,
        IDownloaderSettingsService settingsService,
        IDependencyManagerService dependencyManager,
        IDownloadEventLogService eventLog,
        IDownloadSchedulerService scheduler,
        IDownloaderFileDialogService fileDialog,
        IClipboardService clipboard,
        IUserDialogService dialogs)
    {
        _coordinator = coordinator;
        _assetDiscovery = assetDiscovery;
        _settingsService = settingsService;
        _dependencyManager = dependencyManager;
        _eventLog = eventLog;
        _scheduler = scheduler;
        _fileDialog = fileDialog;
        _clipboard = clipboard;
        _dialogs = dialogs;

        Settings = _settingsService.Load();

        DiscoveredAssetsView = CollectionViewSource.GetDefaultView(DiscoveredAssets);
        DiscoveredAssetsView.Filter = FilterAsset;

        _clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _clipboardTimer.Tick += async (_, _) => await MonitorClipboardAsync();
        ApplyClipboardMonitoring();

        _eventLog.EventRecorded += OnEventRecorded;
        Jobs.CollectionChanged += OnJobsCollectionChanged;

        AddToQueueCommand = new AsyncRelayCommand(_ => AddInputAsync(startNow: false));
        DownloadNowCommand = new AsyncRelayCommand(_ => AddInputAsync(startNow: true));
        ImportLinksCommand = new AsyncRelayCommand(_ => ImportLinksAsync());
        PasteClipboardCommand = new AsyncRelayCommand(_ => PasteClipboardAsync());
        InstallToolsCommand = new AsyncRelayCommand(_ => InstallToolsAsync());
        UpdateToolsCommand = new AsyncRelayCommand(_ => UpdateYtDlpAsync());
        StartQueueCommand = new AsyncRelayCommand(_ => StartQueueAsync());
        PauseQueueCommand = new AsyncRelayCommand(_ => PauseQueueAsync());
        StopQueueCommand = new AsyncRelayCommand(_ => StopQueueAsync());
        RetryFailedCommand = new RelayCommand(_ => RetryFailed());
        ClearCompletedCommand = new RelayCommand(_ => ClearCompleted());
        ClearFailedCommand = new RelayCommand(_ => ClearFailed());
        PauseSelectedCommand = new RelayCommand(_ => _coordinator.PauseJobs(GetSelection()));
        ResumeSelectedCommand = new RelayCommand(_ => _coordinator.ResumeJobs(GetSelection()));
        CancelSelectedCommand = new RelayCommand(_ => _coordinator.CancelJobs(GetSelection()));
        RetrySelectedCommand = new RelayCommand(_ => _coordinator.RetryJobs(GetSelection()));
        RemoveSelectedCommand = new RelayCommand(_ => _coordinator.RemoveJobs(GetSelection()));
        OpenSourceCommand = new RelayCommand(_ => OpenSourceUrl(), _ => SelectedJob is not null);
        OpenContainingFolderCommand = new RelayCommand(_ => OpenContainingFolder(), _ => SelectedJob is not null);
        CopySourceUrlCommand = new RelayCommand(_ => CopySourceUrl(), _ => SelectedJob is not null);
        ScanPageCommand = new AsyncRelayCommand(_ => DiscoverAssetsAsync(false));
        CrawlSiteCommand = new AsyncRelayCommand(_ => DiscoverAssetsAsync(true));
        AddSelectedAssetsCommand = new AsyncRelayCommand(_ => AddSelectedAssetsAsync());
        SelectAllAssetsCommand = new RelayCommand(_ => SetAssetSelection(_ => true));
        SelectVisibleAssetsCommand = new RelayCommand(_ => SetVisibleAssetSelection(true));
        DeselectAllAssetsCommand = new RelayCommand(_ => SetAssetSelection(_ => false));
        InvertAssetSelectionCommand = new RelayCommand(_ => SetAssetSelection(selected => !selected));
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        PickCookieFileCommand = new RelayCommand(_ => PickCookieFile());
        ScheduleStartCommand = new RelayCommand(_ => ScheduleStart());
        SchedulePauseCommand = new RelayCommand(_ => SchedulePause());
        ClearScheduleCommand = new RelayCommand(_ => ClearSchedule());
        ExportDiagnosticsCommand = new AsyncRelayCommand(_ => ExportDiagnosticsAsync());
        ClearHistoryCommand = new RelayCommand(_ => _ = ClearHistoryAsync());
        RedownloadHistoryItemCommand = new AsyncRelayCommand(item => RedownloadHistoryItemAsync(item as DownloadHistoryEntry));

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _coordinator.InitializeAsync();
        RefreshStatistics();
        UpdateSchedulerStatus();
        StatusMessage = DependenciesReady ? "Downloader ready." : "Install downloader tools for media/gallery workflows.";
    }

    private async Task AddInputAsync(bool startNow)
    {
        if (string.IsNullOrWhiteSpace(QuickInput))
        {
            StatusMessage = "Enter one or more URLs first.";
            return;
        }

        SaveSettings();
        var added = await _coordinator.AddFromInputAsync(QuickInput, SelectedMode, startNow);
        RefreshStatistics();
        if (added == 0)
        {
            StatusMessage = "No valid new URLs found in input.";
            return;
        }

        QuickInput = string.Empty;
        StatusMessage = startNow ? $"Added {added} item(s) and started queue." : $"Added {added} item(s) to queue.";
    }

    private async Task ImportLinksAsync()
    {
        var path = _fileDialog.PickImportListFile();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        QuickInput = await File.ReadAllTextAsync(path);
        StatusMessage = $"Imported links from {Path.GetFileName(path)}.";
    }

    private async Task PasteClipboardAsync()
    {
        if (!_clipboard.TryGetText(out var text) || string.IsNullOrWhiteSpace(text))
        {
            StatusMessage = "Clipboard does not contain URL text.";
            return;
        }

        QuickInput = text;
        await AddInputAsync(false);
    }

    private async Task InstallToolsAsync()
    {
        IsInstallingTools = true;
        try
        {
            await _dependencyManager.EnsureAllAsync(msg => StatusMessage = msg);
            StatusMessage = "All downloader tools are installed.";
            OnPropertyChanged(nameof(DependenciesReady));
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Tool install failed", ex.Message);
        }
        finally
        {
            IsInstallingTools = false;
        }
    }

    private async Task UpdateYtDlpAsync()
    {
        var output = await _dependencyManager.UpdateYtDlpAsync();
        StatusMessage = string.IsNullOrWhiteSpace(output) ? "yt-dlp updated." : output;
    }

    private async Task StartQueueAsync() { await _coordinator.StartQueueAsync(); RefreshStatistics(); }

    private async Task PauseQueueAsync() { await _coordinator.PauseQueueAsync(); RefreshStatistics(); }

    private async Task StopQueueAsync() { await _coordinator.StopQueueAsync(); RefreshStatistics(); }

    private void RetryFailed() { _coordinator.RetryJobs(Jobs.Where(j => j.Status == DownloadJobStatus.Failed)); RefreshStatistics(); }

    private void ClearCompleted() { _coordinator.ClearCompleted(); RefreshStatistics(); }

    private void ClearFailed() { _coordinator.ClearFailed(); RefreshStatistics(); }

    private async Task DiscoverAssetsAsync(bool deepCrawl)
    {
        var source = string.IsNullOrWhiteSpace(ScanUrl) ? QuickInput : ScanUrl;
        var url = source.Split(['\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            StatusMessage = "Enter a valid URL for scan/crawl.";
            return;
        }

        IsScanning = true;
        DiscoveredAssets.Clear();
        try
        {
            var progress = new Progress<(int pages, int assets)>(p => ScanStatus = $"Pages: {p.pages}, assets: {p.assets}");
            var assets = await _assetDiscovery.DiscoverAsync(url, deepCrawl, Settings, progress, CancellationToken.None);
            foreach (var asset in assets)
            {
                DiscoveredAssets.Add(asset);
            }

            DiscoveredAssetsView.Refresh();
            ScanStatus = $"Discovery complete: {assets.Count} asset(s).";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task AddSelectedAssetsAsync()
    {
        var selected = DiscoveredAssets.Where(a => a.IsSelected).ToList();
        var added = await _coordinator.AddAssetsAsync(selected, SelectedMode, Settings.General.AutoStartOnAdd);
        RefreshStatistics();
        StatusMessage = $"Added {added} discovered asset(s).";
    }

    private void SaveSettings()
    {
        _settingsService.Save(Settings);
        _coordinator.ReloadSettings();
        ApplyClipboardMonitoring();
    }

    private void PickCookieFile()
    {
        var path = _fileDialog.PickCookieFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            Settings.Connections.CookieFilePath = path;
            SaveSettings();
        }
    }

    private void ScheduleStart()
    {
        if (TryBuildDateTimeOffset(ScheduledStartDate, ScheduledStartTimeText, out var when))
        {
            _scheduler.ScheduleStart(when);
            UpdateSchedulerStatus();
        }
    }

    private void SchedulePause()
    {
        if (TryBuildDateTimeOffset(ScheduledPauseDate, ScheduledPauseTimeText, out var when))
        {
            _scheduler.SchedulePause(when);
            UpdateSchedulerStatus();
        }
    }

    private void ClearSchedule()
    {
        _scheduler.Clear();
        UpdateSchedulerStatus();
    }

    private async Task ExportDiagnosticsAsync()
    {
        var source = await _eventLog.ExportDiagnosticsAsync();
        var target = _fileDialog.PickDiagnosticsExportPath();
        if (!string.IsNullOrWhiteSpace(target))
        {
            File.Copy(source, target, overwrite: true);
            StatusMessage = $"Diagnostics exported to {target}";
        }
    }

    private async Task ClearHistoryAsync()
    {
        if (!_dialogs.Confirm("Clear history", "Delete downloader history entries?"))
        {
            return;
        }

        await _coordinator.StopQueueAsync();
        await _coordinator.ClearHistoryAsync();
        StatusMessage = "History cleared.";
    }

    private async Task RedownloadHistoryItemAsync(DownloadHistoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        QuickInput = entry.SourceUrl;
        SelectedMode = entry.EngineType == DownloadEngineType.Media ? DownloaderMode.MediaDownload : DownloaderMode.QuickDownload;
        await AddInputAsync(true);
    }

    private async Task MonitorClipboardAsync()
    {
        if (!Settings.General.ClipboardMonitoring)
        {
            return;
        }

        if (!_clipboard.TryGetText(out var text) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (string.Equals(text, _lastClipboardText, StringComparison.Ordinal))
        {
            return;
        }

        _lastClipboardText = text;
        var added = await _coordinator.AddFromInputAsync(text, DownloaderMode.QuickDownload, Settings.General.AutoStartOnAdd);
        if (added > 0)
        {
            RefreshStatistics();
            StatusMessage = $"Clipboard monitor added {added} link(s).";
        }
    }

    private void ApplyClipboardMonitoring()
    {
        if (Settings.General.ClipboardMonitoring)
        {
            _clipboardTimer.Start();
        }
        else
        {
            _clipboardTimer.Stop();
        }
    }

    private void OpenSourceUrl()
    {
        if (SelectedJob is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = SelectedJob.SourceUrl,
            UseShellExecute = true,
        });
    }

    private void OpenContainingFolder()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var path = !string.IsNullOrWhiteSpace(SelectedJob.OutputFilePath)
            ? SelectedJob.OutputFilePath
            : SelectedJob.OutputDirectory;

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{path}\"", UseShellExecute = true });
        }
        else if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{path}\"", UseShellExecute = true });
        }
    }

    private void CopySourceUrl()
    {
        if (SelectedJob is not null)
        {
            _clipboard.SetText(SelectedJob.SourceUrl);
            StatusMessage = "Source URL copied to clipboard.";
        }
    }

    private void SetAssetSelection(Func<bool, bool> selector)
    {
        foreach (var asset in DiscoveredAssets)
        {
            asset.IsSelected = selector(asset.IsSelected);
        }
    }

    private void SetVisibleAssetSelection(bool selected)
    {
        foreach (var item in DiscoveredAssetsView)
        {
            if (item is DownloadAssetCandidate asset)
            {
                asset.IsSelected = selected;
            }
        }
    }

    private bool FilterAsset(object obj)
    {
        if (obj is not DownloadAssetCandidate asset)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(AssetSearchText)
            && !asset.Name.Contains(AssetSearchText, StringComparison.OrdinalIgnoreCase)
            && !asset.Url.Contains(AssetSearchText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return SelectedAssetFilter switch
        {
            AssetFilterType.Images => asset.TypeLabel.Equals("Image", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.Video => asset.TypeLabel.Equals("Video", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.Audio => asset.TypeLabel.Equals("Audio", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.Archives => asset.TypeLabel.Equals("Archive", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.Documents => asset.TypeLabel is "Document" or "Spreadsheet" or "Presentation",
            AssetFilterType.Executables => asset.TypeLabel.Equals("Executable", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.CodeTextData => asset.TypeLabel is "Code" or "Text" or "Database",
            AssetFilterType.Fonts => asset.TypeLabel.Equals("Font", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }

    private IEnumerable<DownloadJob> GetSelection() => SelectedJobs.Count > 0 ? SelectedJobs : SelectedJob is null ? [] : [SelectedJob];

    private void OnJobsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is DownloadJob oldJob)
                {
                    oldJob.PropertyChanged -= OnJobPropertyChanged;
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is DownloadJob newJob)
                {
                    newJob.PropertyChanged += OnJobPropertyChanged;
                }
            }
        }

        RefreshStatistics();
    }

    private void OnJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadJob.Status))
        {
            RefreshStatistics();
        }
    }

    private void OnEventRecorded(object? sender, DownloadEventRecord eventRecord)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            RecentEvents.Insert(0, eventRecord);
            while (RecentEvents.Count > 250)
            {
                RecentEvents.RemoveAt(RecentEvents.Count - 1);
            }

            StatusMessage = eventRecord.Message;
        });
    }

    private void RefreshStatistics()
    {
        _coordinator.RecomputeStatistics();
        OnPropertyChanged(nameof(QueuedCount));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(PausedCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(IsQueueRunning));
    }

    private void UpdateSchedulerStatus()
    {
        var start = _scheduler.ScheduledStartAt?.ToLocalTime().ToString("g");
        var pause = _scheduler.ScheduledPauseAt?.ToLocalTime().ToString("g");
        SchedulerStatus = start is null && pause is null ? "No active schedule" : $"Start: {start ?? "-"} | Pause: {pause ?? "-"}";
    }

    private static bool TryBuildDateTimeOffset(DateTime date, string timeText, out DateTimeOffset value)
    {
        value = default;
        if (!TimeSpan.TryParse(timeText, out var time))
        {
            return false;
        }

        var local = new DateTime(date.Year, date.Month, date.Day, time.Hours, time.Minutes, 0, DateTimeKind.Local);
        value = new DateTimeOffset(local);
        return true;
    }
}
