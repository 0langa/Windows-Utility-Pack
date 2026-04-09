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
    private string _mediaInput = string.Empty;
    private string _scanUrl = string.Empty;
    private string _crawlUrl = string.Empty;
    private string _statusMessage = "Ready";
    private string _quickDetectionSummary = "Paste links and choose Add to Queue or Download Now.";
    private string _mediaAnalysisSummary = "Paste media URL and click Analyze Media.";
    private string _scanStatus = string.Empty;
    private string _assetSearchText = string.Empty;
    private AssetFilterType _selectedAssetFilter = AssetFilterType.All;
    private DownloaderMode _selectedMode = DownloaderMode.QuickDownload;
    private MediaOutputKind _selectedMediaOutputKind = MediaOutputKind.Video;
    private string _selectedMediaVideoProfile = "bestvideo+bestaudio/best";
    private string _selectedMediaAudioFormat = "mp3";
    private string _selectedMediaContainer = "mp4";
    private DownloadJob? _selectedJob;
    private DownloadHistoryEntry? _selectedHistoryEntry;
    private DownloadCategoryRule? _selectedCategoryRule;
    private string? _selectedHelpTopic;
    private string _helpContent = string.Empty;
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

    public ObservableCollection<DownloadCategoryRule> CategoryRules { get; } = [];

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

    public IReadOnlyList<MediaOutputKind> MediaOutputKinds { get; } =
    [
        MediaOutputKind.Video,
        MediaOutputKind.AudioOnly,
    ];

    public IReadOnlyList<string> MediaVideoProfiles { get; } =
    [
        "bestvideo+bestaudio/best",
        "bv*[height<=1080]+ba/b[height<=1080]",
        "bv*[height<=720]+ba/b[height<=720]",
        "best",
    ];

    public IReadOnlyList<string> MediaAudioFormats { get; } =
    [
        "mp3",
        "m4a",
        "aac",
        "opus",
        "wav",
    ];

    public IReadOnlyList<string> MediaContainers { get; } =
    [
        "mp4",
        "mkv",
        "webm",
    ];

    public IReadOnlyList<string> HelpTopics { get; } =
    [
        "Quick direct file download",
        "Download a YouTube video as video",
        "Extract audio intentionally",
        "Scan a page for assets",
        "Crawl a site safely",
        "Use queue manager",
        "Use history and redownload",
        "Use categories and rules",
        "Scheduler basics",
        "Diagnostics and logging",
    ];

    public string QuickInput
    {
        get => _quickInput;
        set => SetProperty(ref _quickInput, value);
    }

    public string MediaInput
    {
        get => _mediaInput;
        set => SetProperty(ref _mediaInput, value);
    }

    public string ScanUrl
    {
        get => _scanUrl;
        set => SetProperty(ref _scanUrl, value);
    }

    public string CrawlUrl
    {
        get => _crawlUrl;
        set => SetProperty(ref _crawlUrl, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string QuickDetectionSummary
    {
        get => _quickDetectionSummary;
        set => SetProperty(ref _quickDetectionSummary, value);
    }

    public string MediaAnalysisSummary
    {
        get => _mediaAnalysisSummary;
        set => SetProperty(ref _mediaAnalysisSummary, value);
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

    public MediaOutputKind SelectedMediaOutputKind
    {
        get => _selectedMediaOutputKind;
        set
        {
            if (SetProperty(ref _selectedMediaOutputKind, value))
            {
                UpdateMediaPlanSummary();
            }
        }
    }

    public string SelectedMediaVideoProfile
    {
        get => _selectedMediaVideoProfile;
        set
        {
            if (SetProperty(ref _selectedMediaVideoProfile, value))
            {
                UpdateMediaPlanSummary();
            }
        }
    }

    public string SelectedMediaAudioFormat
    {
        get => _selectedMediaAudioFormat;
        set
        {
            if (SetProperty(ref _selectedMediaAudioFormat, value))
            {
                UpdateMediaPlanSummary();
            }
        }
    }

    public string SelectedMediaContainer
    {
        get => _selectedMediaContainer;
        set
        {
            if (SetProperty(ref _selectedMediaContainer, value))
            {
                UpdateMediaPlanSummary();
            }
        }
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

    public DownloadHistoryEntry? SelectedHistoryEntry
    {
        get => _selectedHistoryEntry;
        set
        {
            if (SetProperty(ref _selectedHistoryEntry, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DownloadCategoryRule? SelectedCategoryRule
    {
        get => _selectedCategoryRule;
        set
        {
            if (SetProperty(ref _selectedCategoryRule, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string HelpContent
    {
        get => _helpContent;
        set => SetProperty(ref _helpContent, value);
    }

    public string? SelectedHelpTopic
    {
        get => _selectedHelpTopic;
        set
        {
            if (SetProperty(ref _selectedHelpTopic, value))
            {
                SelectHelpTopic(value);
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

    public int DiscoveredAssetCount => DiscoveredAssets.Count;

    public int SelectedAssetCount => DiscoveredAssets.Count(asset => asset.IsSelected);

    public AsyncRelayCommand AddToQueueCommand { get; }

    public AsyncRelayCommand DownloadNowCommand { get; }

    public AsyncRelayCommand AnalyzeQuickInputCommand { get; }

    public AsyncRelayCommand AddMediaToQueueCommand { get; }

    public AsyncRelayCommand DownloadMediaNowCommand { get; }

    public AsyncRelayCommand AnalyzeMediaCommand { get; }

    public RelayCommand ResetMediaDefaultsCommand { get; }

    public AsyncRelayCommand ImportLinksCommand { get; }

    public AsyncRelayCommand PasteClipboardCommand { get; }

    public AsyncRelayCommand InstallToolsCommand { get; }

    public AsyncRelayCommand UpdateToolsCommand { get; }

    public AsyncRelayCommand StartQueueCommand { get; }

    public AsyncRelayCommand PauseQueueCommand { get; }

    public AsyncRelayCommand StopQueueCommand { get; }

    public AsyncRelayCommand StartSelectedCommand { get; }

    public RelayCommand RetryFailedCommand { get; }

    public RelayCommand ClearCompletedCommand { get; }

    public RelayCommand ClearFailedCommand { get; }

    public RelayCommand PauseSelectedCommand { get; }

    public RelayCommand ResumeSelectedCommand { get; }

    public RelayCommand CancelSelectedCommand { get; }

    public RelayCommand RetrySelectedCommand { get; }

    public RelayCommand RemoveSelectedCommand { get; }

    public RelayCommand MoveSelectedTopCommand { get; }

    public RelayCommand MoveSelectedUpCommand { get; }

    public RelayCommand MoveSelectedDownCommand { get; }

    public RelayCommand MoveSelectedBottomCommand { get; }

    public RelayCommand SetPriorityHighCommand { get; }

    public RelayCommand SetPriorityNormalCommand { get; }

    public RelayCommand SetPriorityLowCommand { get; }

    public RelayCommand OpenSourceCommand { get; }

    public RelayCommand OpenContainingFolderCommand { get; }

    public RelayCommand CopySourceUrlCommand { get; }

    public AsyncRelayCommand ScanPageCommand { get; }

    public AsyncRelayCommand CrawlSiteCommand { get; }

    public AsyncRelayCommand AddSelectedAssetsCommand { get; }

    public AsyncRelayCommand DownloadSelectedAssetsNowCommand { get; }

    public RelayCommand SelectAllAssetsCommand { get; }

    public RelayCommand SelectVisibleAssetsCommand { get; }

    public RelayCommand DeselectAllAssetsCommand { get; }

    public RelayCommand InvertAssetSelectionCommand { get; }

    public RelayCommand ClearDiscoveredAssetsCommand { get; }

    public RelayCommand SaveSettingsCommand { get; }

    public RelayCommand PickCookieFileCommand { get; }

    public RelayCommand AddCategoryRuleCommand { get; }

    public RelayCommand RemoveCategoryRuleCommand { get; }

    public RelayCommand ResetCategoryRulesCommand { get; }

    public RelayCommand ScheduleStartCommand { get; }

    public RelayCommand SchedulePauseCommand { get; }

    public RelayCommand ClearScheduleCommand { get; }

    public AsyncRelayCommand ExportDiagnosticsCommand { get; }

    public RelayCommand ClearHistoryCommand { get; }

    public AsyncRelayCommand RedownloadHistoryItemCommand { get; }

    public RelayCommand OpenHistoryFileCommand { get; }

    public RelayCommand OpenHistoryFolderCommand { get; }

    public RelayCommand OpenHistorySourceCommand { get; }

    public RelayCommand CopyHistorySourceCommand { get; }

    public RelayCommand SelectHelpTopicCommand { get; }

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
        foreach (var rule in Settings.Categories.Select(CloneRule))
        {
            CategoryRules.Add(rule);
        }

        if (CategoryRules.Count == 0)
        {
            foreach (var rule in DownloadCategoryRule.CreateDefaults().Select(CloneRule))
            {
                CategoryRules.Add(rule);
            }
        }

        SelectedCategoryRule = CategoryRules.FirstOrDefault();
        SelectedMediaVideoProfile = string.IsNullOrWhiteSpace(Settings.Media.PreferredVideoFormat)
            ? "bestvideo+bestaudio/best"
            : Settings.Media.PreferredVideoFormat;
        SelectedMediaAudioFormat = string.IsNullOrWhiteSpace(Settings.Media.PreferredAudioFormat)
            ? "mp3"
            : Settings.Media.PreferredAudioFormat;
        SelectedMediaContainer = MediaContainers.Contains(SelectedMediaContainer, StringComparer.OrdinalIgnoreCase)
            ? SelectedMediaContainer
            : "mp4";
        UpdateMediaPlanSummary();
        SelectedHelpTopic = HelpTopics.FirstOrDefault();

        DiscoveredAssetsView = CollectionViewSource.GetDefaultView(DiscoveredAssets);
        DiscoveredAssetsView.Filter = FilterAsset;

        _clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _clipboardTimer.Tick += async (_, _) => await MonitorClipboardAsync();
        ApplyClipboardMonitoring();

        _eventLog.EventRecorded += OnEventRecorded;
        Jobs.CollectionChanged += OnJobsCollectionChanged;
        DiscoveredAssets.CollectionChanged += OnDiscoveredAssetsChanged;

        AddToQueueCommand = new AsyncRelayCommand(_ => AddQuickInputAsync(startNow: false));
        DownloadNowCommand = new AsyncRelayCommand(_ => AddQuickInputAsync(startNow: true));
        AnalyzeQuickInputCommand = new AsyncRelayCommand(_ => AnalyzeQuickInputAsync());
        AddMediaToQueueCommand = new AsyncRelayCommand(_ => AddMediaInputAsync(startNow: false));
        DownloadMediaNowCommand = new AsyncRelayCommand(_ => AddMediaInputAsync(startNow: true));
        AnalyzeMediaCommand = new AsyncRelayCommand(_ => AnalyzeMediaAsync());
        ResetMediaDefaultsCommand = new RelayCommand(_ => ResetMediaDefaults());
        ImportLinksCommand = new AsyncRelayCommand(_ => ImportLinksAsync());
        PasteClipboardCommand = new AsyncRelayCommand(_ => PasteClipboardAsync());
        InstallToolsCommand = new AsyncRelayCommand(_ => InstallToolsAsync());
        UpdateToolsCommand = new AsyncRelayCommand(_ => UpdateYtDlpAsync());
        StartQueueCommand = new AsyncRelayCommand(_ => StartQueueAsync());
        PauseQueueCommand = new AsyncRelayCommand(_ => PauseQueueAsync());
        StopQueueCommand = new AsyncRelayCommand(_ => StopQueueAsync());
        StartSelectedCommand = new AsyncRelayCommand(_ => StartSelectedAsync());
        RetryFailedCommand = new RelayCommand(_ => RetryFailed());
        ClearCompletedCommand = new RelayCommand(_ => ClearCompleted());
        ClearFailedCommand = new RelayCommand(_ => ClearFailed());
        PauseSelectedCommand = new RelayCommand(_ => _coordinator.PauseJobs(GetSelection()));
        ResumeSelectedCommand = new RelayCommand(_ => _coordinator.ResumeJobs(GetSelection()));
        CancelSelectedCommand = new RelayCommand(_ => _coordinator.CancelJobs(GetSelection()));
        RetrySelectedCommand = new RelayCommand(_ => _coordinator.RetryJobs(GetSelection()));
        RemoveSelectedCommand = new RelayCommand(_ => _coordinator.RemoveJobs(GetSelection()));
        MoveSelectedTopCommand = new RelayCommand(_ => _coordinator.MoveJobsToTop(GetSelection()));
        MoveSelectedUpCommand = new RelayCommand(_ => _coordinator.MoveJobsUp(GetSelection()));
        MoveSelectedDownCommand = new RelayCommand(_ => _coordinator.MoveJobsDown(GetSelection()));
        MoveSelectedBottomCommand = new RelayCommand(_ => _coordinator.MoveJobsToBottom(GetSelection()));
        SetPriorityHighCommand = new RelayCommand(_ => _coordinator.SetPriority(GetSelection(), DownloadPriority.High));
        SetPriorityNormalCommand = new RelayCommand(_ => _coordinator.SetPriority(GetSelection(), DownloadPriority.Normal));
        SetPriorityLowCommand = new RelayCommand(_ => _coordinator.SetPriority(GetSelection(), DownloadPriority.Low));
        OpenSourceCommand = new RelayCommand(_ => OpenSourceUrl(), _ => SelectedJob is not null);
        OpenContainingFolderCommand = new RelayCommand(_ => OpenContainingFolder(), _ => SelectedJob is not null);
        CopySourceUrlCommand = new RelayCommand(_ => CopySourceUrl(), _ => SelectedJob is not null);
        ScanPageCommand = new AsyncRelayCommand(_ => DiscoverAssetsAsync(ScanUrl, false));
        CrawlSiteCommand = new AsyncRelayCommand(_ => DiscoverAssetsAsync(CrawlUrl, true));
        AddSelectedAssetsCommand = new AsyncRelayCommand(_ => AddSelectedAssetsAsync(startNow: false));
        DownloadSelectedAssetsNowCommand = new AsyncRelayCommand(_ => AddSelectedAssetsAsync(startNow: true));
        SelectAllAssetsCommand = new RelayCommand(_ => SetAssetSelection(_ => true));
        SelectVisibleAssetsCommand = new RelayCommand(_ => SetVisibleAssetSelection(true));
        DeselectAllAssetsCommand = new RelayCommand(_ => SetAssetSelection(_ => false));
        InvertAssetSelectionCommand = new RelayCommand(_ => SetAssetSelection(selected => !selected));
        ClearDiscoveredAssetsCommand = new RelayCommand(_ => ClearDiscoveredAssets());
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        PickCookieFileCommand = new RelayCommand(_ => PickCookieFile());
        AddCategoryRuleCommand = new RelayCommand(_ => AddCategoryRule());
        RemoveCategoryRuleCommand = new RelayCommand(_ => RemoveCategoryRule(), _ => SelectedCategoryRule is not null);
        ResetCategoryRulesCommand = new RelayCommand(_ => ResetCategoryRules());
        ScheduleStartCommand = new RelayCommand(_ => ScheduleStart());
        SchedulePauseCommand = new RelayCommand(_ => SchedulePause());
        ClearScheduleCommand = new RelayCommand(_ => ClearSchedule());
        ExportDiagnosticsCommand = new AsyncRelayCommand(_ => ExportDiagnosticsAsync());
        ClearHistoryCommand = new RelayCommand(_ => _ = ClearHistoryAsync());
        RedownloadHistoryItemCommand = new AsyncRelayCommand(item => RedownloadHistoryItemAsync(item as DownloadHistoryEntry ?? SelectedHistoryEntry));
        OpenHistoryFileCommand = new RelayCommand(_ => OpenHistoryFile(), _ => SelectedHistoryEntry is not null);
        OpenHistoryFolderCommand = new RelayCommand(_ => OpenHistoryFolder(), _ => SelectedHistoryEntry is not null);
        OpenHistorySourceCommand = new RelayCommand(_ => OpenHistorySource(), _ => SelectedHistoryEntry is not null);
        CopyHistorySourceCommand = new RelayCommand(_ => CopyHistorySource(), _ => SelectedHistoryEntry is not null);
        SelectHelpTopicCommand = new RelayCommand(topic => SelectHelpTopic(topic as string));

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _coordinator.InitializeAsync();
        RefreshStatistics();
        UpdateSchedulerStatus();
        StatusMessage = DependenciesReady ? "Downloader ready." : "Install downloader tools for media/gallery workflows.";
    }

    private async Task AddQuickInputAsync(bool startNow)
    {
        if (string.IsNullOrWhiteSpace(QuickInput))
        {
            StatusMessage = "Enter one or more URLs first.";
            return;
        }

        SaveSettings();
        var added = await _coordinator.AddFromInputAsync(QuickInput, DownloaderMode.QuickDownload, startNow);
        RefreshStatistics();
        if (added == 0)
        {
            StatusMessage = "No valid new URLs found in input.";
            return;
        }

        await AnalyzeQuickInputAsync();
        if (startNow)
        {
            QuickInput = string.Empty;
        }

        StatusMessage = startNow ? $"Added {added} item(s) and started queue." : $"Added {added} item(s) to queue.";
    }

    private async Task AnalyzeQuickInputAsync()
    {
        await Task.Yield();
        QuickDetectionSummary = TryExtractFirstNormalizedUrl(QuickInput, out var url)
            ? $"Detected workflow: {PredictWorkflow(url, DownloaderMode.QuickDownload)}"
            : "Enter a valid URL to preview detected workflow.";
    }

    private async Task AddMediaInputAsync(bool startNow)
    {
        if (!TryExtractFirstNormalizedUrl(MediaInput, out _))
        {
            StatusMessage = "Enter a valid media URL.";
            return;
        }

        SaveSettings();

        var existing = Jobs.Select(job => job.JobId).ToHashSet();
        var added = await _coordinator.AddFromInputAsync(MediaInput, DownloaderMode.MediaDownload, startImmediately: false);

        foreach (var job in Jobs.Where(job => !existing.Contains(job.JobId) && job.Mode == DownloaderMode.MediaDownload))
        {
            ApplyMediaSelection(job, startNow || Settings.General.AutoStartOnAdd);
        }

        if (startNow || Settings.General.AutoStartOnAdd)
        {
            await _coordinator.StartQueueAsync();
        }

        RefreshStatistics();
        await AnalyzeMediaAsync();
        StatusMessage = added == 0
            ? "No new media links were added."
            : startNow
                ? $"Added {added} media item(s) and started queue."
                : $"Added {added} media item(s) to queue.";
    }

    private async Task AnalyzeMediaAsync()
    {
        await Task.Yield();
        if (!TryExtractFirstNormalizedUrl(MediaInput, out var url))
        {
            MediaAnalysisSummary = "Enter a valid media URL first.";
            return;
        }

        MediaAnalysisSummary = SelectedMediaOutputKind == MediaOutputKind.AudioOnly
            ? $"Workflow: {PredictWorkflow(url, DownloaderMode.MediaDownload)} | Audio only ({SelectedMediaAudioFormat.ToUpperInvariant()})"
            : $"Workflow: {PredictWorkflow(url, DownloaderMode.MediaDownload)} | Video ({SelectedMediaContainer.ToUpperInvariant()}, {SelectedMediaVideoProfile})";
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
        await AnalyzeQuickInputAsync();
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

    private async Task StartSelectedAsync()
    {
        await Task.Yield();
        foreach (var job in GetSelection())
        {
            if (job.Status is DownloadJobStatus.Staged or DownloadJobStatus.Paused)
            {
                job.Status = DownloadJobStatus.Queued;
                job.StatusMessage = "Queued";
            }
            else if (job.Status is DownloadJobStatus.Failed or DownloadJobStatus.Cancelled)
            {
                _coordinator.RetryJobs([job]);
            }
        }

        await _coordinator.StartQueueAsync();
        RefreshStatistics();
    }

    private void RetryFailed() { _coordinator.RetryJobs(Jobs.Where(j => j.Status == DownloadJobStatus.Failed)); RefreshStatistics(); }

    private void ClearCompleted() { _coordinator.ClearCompleted(); RefreshStatistics(); }

    private void ClearFailed() { _coordinator.ClearFailed(); RefreshStatistics(); }

    private void ApplyMediaSelection(DownloadJob job, bool queueNow)
    {
        job.MediaOutputKind = SelectedMediaOutputKind;
        job.SelectedContainer = string.IsNullOrWhiteSpace(SelectedMediaContainer) ? "mp4" : SelectedMediaContainer;

        if (SelectedMediaOutputKind == MediaOutputKind.AudioOnly)
        {
            job.SelectedProfile = "bestaudio/best";
            job.EffectivePlan = $"Audio only: {SelectedMediaAudioFormat.ToUpperInvariant()}";
        }
        else
        {
            job.SelectedProfile = string.IsNullOrWhiteSpace(SelectedMediaVideoProfile)
                ? "bestvideo+bestaudio/best"
                : SelectedMediaVideoProfile;
            job.EffectivePlan = $"Video: {job.SelectedContainer.ToUpperInvariant()} ({job.SelectedProfile})";
        }

        if (queueNow && job.Status is DownloadJobStatus.Staged or DownloadJobStatus.Paused)
        {
            job.Status = DownloadJobStatus.Queued;
            job.StatusMessage = "Queued";
        }
    }

    private void ResetMediaDefaults()
    {
        SelectedMediaOutputKind = MediaOutputKind.Video;
        SelectedMediaVideoProfile = "bestvideo+bestaudio/best";
        SelectedMediaAudioFormat = "mp3";
        SelectedMediaContainer = "mp4";
        Settings.Media.DownloadSubtitles = false;
        Settings.Media.DownloadThumbnail = false;
        Settings.Media.EmbedMetadata = true;
        Settings.Media.AllowPlaylist = false;
        UpdateMediaPlanSummary();
    }

    private async Task DiscoverAssetsAsync(string inputUrl, bool deepCrawl)
    {
        if (!TryExtractFirstNormalizedUrl(inputUrl, out var url))
        {
            StatusMessage = deepCrawl ? "Enter a valid root URL for crawl." : "Enter a valid page URL for scan.";
            return;
        }

        IsScanning = true;
        try
        {
            var progress = new Progress<(int pages, int assets)>(p => ScanStatus = $"Pages: {p.pages}, assets: {p.assets}");
            var assets = await _assetDiscovery.DiscoverAsync(url, deepCrawl, Settings, progress, CancellationToken.None);
            var existing = DiscoveredAssets.Select(item => item.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = 0;

            foreach (var asset in assets)
            {
                if (existing.Add(asset.Url))
                {
                    DiscoveredAssets.Add(asset);
                    added++;
                }
            }

            DiscoveredAssetsView.Refresh();
            ScanStatus = deepCrawl
                ? $"Crawl complete: {assets.Count} found, {added} new staged item(s)."
                : $"Scan complete: {assets.Count} found, {added} new staged item(s).";
            OnPropertyChanged(nameof(DiscoveredAssetCount));
            OnPropertyChanged(nameof(SelectedAssetCount));
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task AddSelectedAssetsAsync(bool startNow)
    {
        var selected = DiscoveredAssets.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Select one or more discovered assets first.";
            return;
        }

        var added = await _coordinator.AddAssetsAsync(selected, DownloaderMode.QuickDownload, startNow);
        RefreshStatistics();
        StatusMessage = startNow
            ? $"Added {added} discovered asset(s) and started queue."
            : $"Added {added} discovered asset(s) to queue.";
    }

    private void SaveSettings()
    {
        Settings.Media.PreferredVideoFormat = string.IsNullOrWhiteSpace(SelectedMediaVideoProfile)
            ? "bestvideo+bestaudio/best"
            : SelectedMediaVideoProfile;
        Settings.Media.PreferredAudioFormat = string.IsNullOrWhiteSpace(SelectedMediaAudioFormat)
            ? "mp3"
            : SelectedMediaAudioFormat;
        Settings.Categories = CategoryRules.Select(CloneRule).ToList();
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

    private void AddCategoryRule()
    {
        var rule = new DownloadCategoryRule
        {
            Name = "New Category",
            Extensions = [],
            DomainContains = [],
            PriorityOverride = DownloadPriority.Normal,
        };

        CategoryRules.Add(rule);
        SelectedCategoryRule = rule;
    }

    private void RemoveCategoryRule()
    {
        if (SelectedCategoryRule is null)
        {
            return;
        }

        CategoryRules.Remove(SelectedCategoryRule);
        SelectedCategoryRule = CategoryRules.FirstOrDefault();
    }

    private void ResetCategoryRules()
    {
        CategoryRules.Clear();
        foreach (var rule in DownloadCategoryRule.CreateDefaults().Select(CloneRule))
        {
            CategoryRules.Add(rule);
        }

        SelectedCategoryRule = CategoryRules.FirstOrDefault();
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

        if (entry.EngineType == DownloadEngineType.Media)
        {
            MediaInput = entry.SourceUrl;
            await AddMediaInputAsync(true);
            return;
        }

        QuickInput = entry.SourceUrl;
        await AddQuickInputAsync(true);
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

    private void OpenHistoryFile()
    {
        if (SelectedHistoryEntry is null || string.IsNullOrWhiteSpace(SelectedHistoryEntry.OutputFilePath))
        {
            return;
        }

        if (File.Exists(SelectedHistoryEntry.OutputFilePath))
        {
            Process.Start(new ProcessStartInfo { FileName = SelectedHistoryEntry.OutputFilePath, UseShellExecute = true });
        }
    }

    private void OpenHistoryFolder()
    {
        if (SelectedHistoryEntry is null || string.IsNullOrWhiteSpace(SelectedHistoryEntry.OutputFilePath))
        {
            return;
        }

        var path = SelectedHistoryEntry.OutputFilePath;
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{path}\"", UseShellExecute = true });
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{directory}\"", UseShellExecute = true });
        }
    }

    private void OpenHistorySource()
    {
        if (SelectedHistoryEntry is not null)
        {
            Process.Start(new ProcessStartInfo { FileName = SelectedHistoryEntry.SourceUrl, UseShellExecute = true });
        }
    }

    private void CopyHistorySource()
    {
        if (SelectedHistoryEntry is not null)
        {
            _clipboard.SetText(SelectedHistoryEntry.SourceUrl);
            StatusMessage = "History URL copied to clipboard.";
        }
    }

    private void SetAssetSelection(Func<bool, bool> selector)
    {
        foreach (var asset in DiscoveredAssets)
        {
            asset.IsSelected = selector(asset.IsSelected);
        }

        OnPropertyChanged(nameof(SelectedAssetCount));
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

        OnPropertyChanged(nameof(SelectedAssetCount));
    }

    private void ClearDiscoveredAssets()
    {
        DiscoveredAssets.Clear();
        DiscoveredAssetsView.Refresh();
        OnPropertyChanged(nameof(DiscoveredAssetCount));
        OnPropertyChanged(nameof(SelectedAssetCount));
        ScanStatus = "Discovery staging cleared.";
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

    private void OnDiscoveredAssetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is DownloadAssetCandidate oldAsset)
                {
                    oldAsset.PropertyChanged -= OnDiscoveredAssetPropertyChanged;
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is DownloadAssetCandidate newAsset)
                {
                    newAsset.PropertyChanged += OnDiscoveredAssetPropertyChanged;
                }
            }
        }

        OnPropertyChanged(nameof(DiscoveredAssetCount));
        OnPropertyChanged(nameof(SelectedAssetCount));
    }

    private void OnDiscoveredAssetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadAssetCandidate.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedAssetCount));
        }
    }

    private void UpdateMediaPlanSummary()
    {
        MediaAnalysisSummary = SelectedMediaOutputKind == MediaOutputKind.AudioOnly
            ? $"Planned output: Audio only ({SelectedMediaAudioFormat.ToUpperInvariant()})"
            : $"Planned output: Video ({SelectedMediaContainer.ToUpperInvariant()}, {SelectedMediaVideoProfile})";
    }

    private void SelectHelpTopic(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        HelpContent = topic switch
        {
            "Quick direct file download" => "Open Quick Download, paste links, then Add to Queue or Download Now.",
            "Download a YouTube video as video" => "Use Media tab. Keep Output Mode = Video (default), then analyze and start.",
            "Extract audio intentionally" => "Switch Output Mode to Audio only before starting.",
            "Scan a page for assets" => "Use Asset Grabber: URL -> Scan Page -> select assets -> queue/download.",
            "Crawl a site safely" => "Use Site Crawl with same-domain and depth/page limits.",
            "Use queue manager" => "Queue Manager supports start/pause/stop, reorder and priority controls.",
            "Use history and redownload" => "History & Diagnostics supports redownload and source/file/folder actions.",
            "Use categories and rules" => "Edit category rules in Settings to auto-route extensions/domains.",
            "Scheduler basics" => "Set one-time queue start/pause time and monitor scheduler status.",
            "Diagnostics and logging" => "Filter recent events and export diagnostics for troubleshooting.",
            _ => topic,
        };
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

    private static DownloadCategoryRule CloneRule(DownloadCategoryRule rule)
    {
        return new DownloadCategoryRule
        {
            Name = rule.Name,
            DefaultFolder = rule.DefaultFolder,
            Extensions = [.. rule.Extensions],
            DomainContains = [.. rule.DomainContains],
            PriorityOverride = rule.PriorityOverride,
        };
    }

    private static bool TryExtractFirstNormalizedUrl(string input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var tokens = input.Split(['\r', '\n', '\t', ' ', ';', ','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var candidate = token.Trim().Trim('"', '\'', '<', '>', '(', ')', '[', ']', '{', '}');
            if (candidate.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                candidate = "https://" + candidate;
            }

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrWhiteSpace(uri.Host))
            {
                normalized = uri.ToString();
                return true;
            }
        }

        return false;
    }

    private static string PredictWorkflow(string url, DownloaderMode mode)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "Fallback direct";
        }

        if (mode == DownloaderMode.MediaDownload)
        {
            return "Media extraction";
        }

        var host = uri.Host;
        if (host.Contains("youtube", StringComparison.OrdinalIgnoreCase)
            || host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)
            || host.Contains("vimeo", StringComparison.OrdinalIgnoreCase)
            || host.Contains("twitch", StringComparison.OrdinalIgnoreCase)
            || host.Contains("soundcloud", StringComparison.OrdinalIgnoreCase))
        {
            return "Media extraction";
        }

        if (host.Contains("imgur", StringComparison.OrdinalIgnoreCase)
            || host.Contains("reddit", StringComparison.OrdinalIgnoreCase)
            || host.Contains("flickr", StringComparison.OrdinalIgnoreCase)
            || host.Contains("deviantart", StringComparison.OrdinalIgnoreCase)
            || host.Contains("pixiv", StringComparison.OrdinalIgnoreCase)
            || host.Contains("tumblr", StringComparison.OrdinalIgnoreCase))
        {
            return "Gallery/collection";
        }

        return mode switch
        {
            DownloaderMode.AssetGrabber => "Asset scan",
            DownloaderMode.SiteCrawl => "Site crawl",
            _ => "Direct file",
        };
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
