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
    private string _youtubeInput = string.Empty;
    private string _scanUrl = string.Empty;
    private string _crawlUrl = string.Empty;
    private string _statusMessage = "Ready";
    private string _quickDetectionSummary = "Paste links and choose Add to Queue or Download Now.";
    private string _mediaAnalysisSummary = "Paste media URL and click Analyze Media.";
    private string _youtubeAnalysisSummary = "Paste YouTube URL, pick quality, then Analyze or Download.";
    private string _scanStatus = string.Empty;
    private string _assetSearchText = string.Empty;
    private AssetFilterType _selectedAssetFilter = AssetFilterType.All;
    private DownloaderMode _selectedMode = DownloaderMode.QuickDownload;
    private MediaOutputKind _selectedMediaOutputKind = MediaOutputKind.Video;
    private string _selectedMediaVideoProfile = "bestvideo+bestaudio/best";
    private string _selectedMediaAudioFormat = "mp3";
    private string _selectedMediaContainer = "mp4";
    private string _selectedYoutubeVideoQuality = "1080p (Full HD)";
    private string _selectedYoutubeFrameRate = "Up to 60 fps";
    private string _selectedYoutubeVideoCodec = "Any codec";
    private string _selectedYoutubeAudioQuality = "Best available";
    private string _selectedYoutubeAudioCodec = "Best available";
    private string _selectedYoutubeContainer = "mp4";
    private DownloadJob? _selectedJob;
    private DownloadHistoryEntry? _selectedHistoryEntry;
    private DownloadCategoryRule? _selectedCategoryRule;
    private string? _selectedHelpTopic;
    private string _helpSearchText = string.Empty;
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

    public IReadOnlyList<string> YoutubeVideoQualities { get; } =
    [
        "Best available",
        "2160p (4K)",
        "1440p",
        "1080p (Full HD)",
        "720p (HD)",
        "480p",
        "360p",
    ];

    public IReadOnlyList<string> YoutubeFrameRates { get; } =
    [
        "Any",
        "Up to 60 fps",
        "Up to 30 fps",
    ];

    public IReadOnlyList<string> YoutubeVideoCodecs { get; } =
    [
        "Any codec",
        "H.264 (AVC)",
        "VP9",
        "AV1",
    ];

    public IReadOnlyList<string> YoutubeAudioQualities { get; } =
    [
        "Best available",
        "High (~256 kbps)",
        "Balanced (~192 kbps)",
        "Compact (~128 kbps)",
        "Small (~96 kbps)",
    ];

    public IReadOnlyList<string> YoutubeAudioCodecs { get; } =
    [
        "Best available",
        "M4A (AAC)",
        "Opus",
        "Vorbis",
    ];

    public ObservableCollection<string> HelpTopics { get; } =
    [
        "Start Here: Workflow Overview",
        "Quick direct file download",
        "YouTube quality tab",
        "Download a YouTube video as video",
        "Extract audio intentionally",
        "Scan a page for assets",
        "Crawl a site safely",
        "Use queue manager",
        "Queue states and what they mean",
        "Use history and redownload",
        "Use categories and rules",
        "Scheduler basics",
        "Diagnostics and logging",
        "Troubleshooting common issues",
        "Feature validation checklist",
    ];

    public ICollectionView HelpTopicsView { get; }

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

    public string YoutubeInput
    {
        get => _youtubeInput;
        set => SetProperty(ref _youtubeInput, value);
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

    public string YoutubeAnalysisSummary
    {
        get => _youtubeAnalysisSummary;
        set => SetProperty(ref _youtubeAnalysisSummary, value);
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

    public string SelectedYoutubeVideoQuality
    {
        get => _selectedYoutubeVideoQuality;
        set
        {
            if (SetProperty(ref _selectedYoutubeVideoQuality, value))
            {
                UpdateYoutubePlanSummary();
            }
        }
    }

    public string SelectedYoutubeFrameRate
    {
        get => _selectedYoutubeFrameRate;
        set
        {
            if (SetProperty(ref _selectedYoutubeFrameRate, value))
            {
                UpdateYoutubePlanSummary();
            }
        }
    }

    public string SelectedYoutubeVideoCodec
    {
        get => _selectedYoutubeVideoCodec;
        set
        {
            if (SetProperty(ref _selectedYoutubeVideoCodec, value))
            {
                UpdateYoutubePlanSummary();
            }
        }
    }

    public string SelectedYoutubeAudioQuality
    {
        get => _selectedYoutubeAudioQuality;
        set
        {
            if (SetProperty(ref _selectedYoutubeAudioQuality, value))
            {
                UpdateYoutubePlanSummary();
            }
        }
    }

    public string SelectedYoutubeAudioCodec
    {
        get => _selectedYoutubeAudioCodec;
        set
        {
            if (SetProperty(ref _selectedYoutubeAudioCodec, value))
            {
                UpdateYoutubePlanSummary();
            }
        }
    }

    public string SelectedYoutubeContainer
    {
        get => _selectedYoutubeContainer;
        set
        {
            if (SetProperty(ref _selectedYoutubeContainer, value))
            {
                UpdateYoutubePlanSummary();
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

    public string HelpSearchText
    {
        get => _helpSearchText;
        set
        {
            if (SetProperty(ref _helpSearchText, value))
            {
                HelpTopicsView.Refresh();
            }
        }
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

    public AsyncRelayCommand AnalyzeYoutubeCommand { get; }

    public AsyncRelayCommand AddYoutubeToQueueCommand { get; }

    public AsyncRelayCommand DownloadYoutubeNowCommand { get; }

    public RelayCommand ResetYoutubeDefaultsCommand { get; }

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
        SelectedYoutubeVideoQuality = string.IsNullOrWhiteSpace(Settings.Media.PreferredYouTubeVideoQuality)
            ? "1080p (Full HD)"
            : Settings.Media.PreferredYouTubeVideoQuality;
        SelectedYoutubeFrameRate = string.IsNullOrWhiteSpace(Settings.Media.PreferredYouTubeFrameRate)
            ? "Up to 60 fps"
            : Settings.Media.PreferredYouTubeFrameRate;
        SelectedYoutubeVideoCodec = string.IsNullOrWhiteSpace(Settings.Media.PreferredYouTubeVideoCodec)
            ? "Any codec"
            : Settings.Media.PreferredYouTubeVideoCodec;
        SelectedYoutubeAudioQuality = string.IsNullOrWhiteSpace(Settings.Media.PreferredYouTubeAudioQuality)
            ? "Best available"
            : Settings.Media.PreferredYouTubeAudioQuality;
        SelectedYoutubeAudioCodec = string.IsNullOrWhiteSpace(Settings.Media.PreferredYouTubeAudioCodec)
            ? "Best available"
            : Settings.Media.PreferredYouTubeAudioCodec;
        SelectedYoutubeContainer = MediaContainers.Contains(Settings.Media.PreferredYouTubeContainer, StringComparer.OrdinalIgnoreCase)
            ? Settings.Media.PreferredYouTubeContainer
            : "mp4";
        UpdateMediaPlanSummary();
        UpdateYoutubePlanSummary();
        SelectedHelpTopic = HelpTopics.FirstOrDefault();

        DiscoveredAssetsView = CollectionViewSource.GetDefaultView(DiscoveredAssets);
        DiscoveredAssetsView.Filter = FilterAsset;

        HelpTopicsView = CollectionViewSource.GetDefaultView(HelpTopics);
        HelpTopicsView.Filter = FilterHelpTopic;

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
        AnalyzeYoutubeCommand = new AsyncRelayCommand(_ => AnalyzeYoutubeAsync());
        AddYoutubeToQueueCommand = new AsyncRelayCommand(_ => AddYoutubeInputAsync(startNow: false));
        DownloadYoutubeNowCommand = new AsyncRelayCommand(_ => AddYoutubeInputAsync(startNow: true));
        ResetYoutubeDefaultsCommand = new RelayCommand(_ => ResetYoutubeDefaults());
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

    private async Task AddYoutubeInputAsync(bool startNow)
    {
        var urls = ExtractNormalizedUrls(YoutubeInput);
        if (urls.Count == 0)
        {
            StatusMessage = "Enter one or more valid YouTube URLs.";
            return;
        }

        var youtubeUrls = urls.Where(YouTubeDownloadPlanBuilder.IsYouTubeUrl).ToList();
        if (youtubeUrls.Count == 0)
        {
            StatusMessage = "This tab accepts youtube.com or youtu.be links only.";
            return;
        }

        SaveSettings();

        var existing = Jobs.Select(job => job.JobId).ToHashSet();
        var addInput = string.Join(Environment.NewLine, youtubeUrls);
        var added = await _coordinator.AddFromInputAsync(addInput, DownloaderMode.MediaDownload, startImmediately: false);

        foreach (var job in Jobs.Where(job => !existing.Contains(job.JobId) && job.Mode == DownloaderMode.MediaDownload))
        {
            if (!YouTubeDownloadPlanBuilder.IsYouTubeUrl(job.SourceUrl))
            {
                continue;
            }

            ApplyYouTubeSelection(job, startNow || Settings.General.AutoStartOnAdd);
        }

        if (startNow || Settings.General.AutoStartOnAdd)
        {
            await _coordinator.StartQueueAsync();
        }

        RefreshStatistics();
        await AnalyzeYoutubeAsync();

        var ignored = urls.Count - youtubeUrls.Count;
        StatusMessage = added == 0
            ? "No new YouTube links were added."
            : startNow
                ? $"Added {added} YouTube item(s) and started queue{FormatIgnoredMessage(ignored)}."
                : $"Added {added} YouTube item(s) to queue{FormatIgnoredMessage(ignored)}.";
    }

    private async Task AnalyzeYoutubeAsync()
    {
        await Task.Yield();
        if (!TryExtractFirstNormalizedUrl(YoutubeInput, out var url))
        {
            YoutubeAnalysisSummary = "Paste a valid youtube.com or youtu.be link.";
            return;
        }

        if (!YouTubeDownloadPlanBuilder.IsYouTubeUrl(url))
        {
            YoutubeAnalysisSummary = "Detected non-YouTube link. This tab is optimized for YouTube only.";
            return;
        }

        YoutubeAnalysisSummary = YouTubeDownloadPlanBuilder.BuildSummary(BuildYouTubeOptions());
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

    private void ApplyYouTubeSelection(DownloadJob job, bool queueNow)
    {
        var options = BuildYouTubeOptions();
        job.MediaOutputKind = MediaOutputKind.Video;
        job.SelectedContainer = string.IsNullOrWhiteSpace(options.Container) ? "mp4" : options.Container;
        job.SelectedProfile = YouTubeDownloadPlanBuilder.BuildFormatExpression(options);
        job.EffectivePlan = YouTubeDownloadPlanBuilder.BuildSummary(options);

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

    private void ResetYoutubeDefaults()
    {
        SelectedYoutubeVideoQuality = "1080p (Full HD)";
        SelectedYoutubeFrameRate = "Up to 60 fps";
        SelectedYoutubeVideoCodec = "Any codec";
        SelectedYoutubeAudioQuality = "Best available";
        SelectedYoutubeAudioCodec = "Best available";
        SelectedYoutubeContainer = "mp4";
        UpdateYoutubePlanSummary();
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
        Settings.Media.PreferredYouTubeVideoQuality = SelectedYoutubeVideoQuality;
        Settings.Media.PreferredYouTubeFrameRate = SelectedYoutubeFrameRate;
        Settings.Media.PreferredYouTubeVideoCodec = SelectedYoutubeVideoCodec;
        Settings.Media.PreferredYouTubeAudioQuality = SelectedYoutubeAudioQuality;
        Settings.Media.PreferredYouTubeAudioCodec = SelectedYoutubeAudioCodec;
        Settings.Media.PreferredYouTubeContainer = string.IsNullOrWhiteSpace(SelectedYoutubeContainer)
            ? "mp4"
            : SelectedYoutubeContainer;
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

    private void UpdateYoutubePlanSummary()
    {
        YoutubeAnalysisSummary = YouTubeDownloadPlanBuilder.BuildSummary(BuildYouTubeOptions());
    }

    private YouTubeDownloadOptions BuildYouTubeOptions()
    {
        return new YouTubeDownloadOptions
        {
            VideoQuality = SelectedYoutubeVideoQuality,
            FrameRate = SelectedYoutubeFrameRate,
            VideoCodec = SelectedYoutubeVideoCodec,
            AudioQuality = SelectedYoutubeAudioQuality,
            AudioCodec = SelectedYoutubeAudioCodec,
            Container = string.IsNullOrWhiteSpace(SelectedYoutubeContainer) ? "mp4" : SelectedYoutubeContainer,
        };
    }

    private void SelectHelpTopic(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        HelpContent = topic switch
        {
            "Start Here: Workflow Overview" => "Downloader Studio has eight workspaces:\n\n1) Quick Download\nDirect file and mixed URL input. Fast add/start with auto engine selection.\n\n2) Queue Manager\nMain control center for live jobs: start/pause/stop, retry, reorder, priority, inspector.\n\n3) Media Download\nExplicit media workflow. Video is the default output. Audio-only must be selected manually.\n\n4) YouTube Video\nFocused YouTube flow with clear quality controls for video resolution/FPS/codec and audio quality/codec.\n\n5) Asset Grabber\nScan one page, review staged assets, select items, then queue/download selected.\n\n6) Site Crawl\nControlled multi-page discovery with scope limits (domain, depth, max pages, workers).\n\n7) History & Diagnostics\nReview completed/failed jobs, redownload, open source/file/folder, inspect recent events.\n\n8) Settings, Rules & Help\nPersist behavior, manage category rules, scheduler, and troubleshooting guidance.\n\nExpected result:\n- You always know what action will happen in each tab.\n- Discovery tabs stage results first; they do not silently do fallback direct downloads.",
            "Quick direct file download" => "Steps:\n1) Open Quick Download.\n2) Paste one or multiple URLs.\n3) Click Analyze Input to preview detected workflow.\n4) Click Add to Queue for staged processing or Download Now for immediate start.\n\nWhat works here:\n- Multi-line and noisy input parsing.\n- URL normalization (including www.* links).\n- Auto engine routing for quick mode.\n\nExpected result:\n- Status bar confirms added item count.\n- New jobs appear in Queue Manager with clear Engine/Status/Plan.",
            "YouTube quality tab" => "Steps:\n1) Open YouTube Video.\n2) Paste one or more YouTube links.\n3) Choose video quality, FPS, video codec, audio quality, audio codec, and container.\n4) Click Analyze YouTube.\n5) Click Add to Queue or Download Now.\n\nWhat this tab guarantees:\n- YouTube-only routing (non-YouTube links are ignored).\n- Explicit quality plan shown before start.\n- Output stays video-focused with selected quality profile.\n\nExpected result:\n- Plan text clearly states selected video/audio quality profile.\n- Queue entry carries an explicit YouTube format expression.",
            "Download a YouTube video as video" => "Steps:\n1) Open Media Download.\n2) Paste a YouTube/media URL.\n3) Keep Output = Video (default).\n4) Choose Video Profile and Container.\n5) Click Analyze Media.\n6) Click Add to Queue or Download Now.\n\nImportant:\n- Media defaults to VIDEO output.\n- No hidden audio-only carry-over is applied.\n\nExpected result:\n- Media analysis text shows a video plan.\n- Queue item shows plan like 'Video: MP4 (...)'.",
            "Extract audio intentionally" => "Steps:\n1) Open Media Download.\n2) Set Output = AudioOnly.\n3) Choose Audio Format.\n4) Analyze Media.\n5) Add to Queue or Download Now.\n\nSafety behavior:\n- Audio extraction only happens when AudioOnly is explicitly selected.\n- Video mode does not auto-switch to audio mode.\n\nExpected result:\n- Analysis text explicitly says audio-only.\n- Queue job plan indicates audio-only output.",
            "Scan a page for assets" => "Steps:\n1) Open Asset Grabber.\n2) Paste one page URL.\n3) Click Scan Page.\n4) Filter/search results.\n5) Use Select All / Select Visible / Invert.\n6) Click Add Selected to Queue or Download Selected Now.\n\nResult columns:\n- Name, Type, Extension, Size, Source Page, Host, Reachable, Warning.\n\nExpected result:\n- Discovered assets appear in staged list.\n- Only selected assets are queued/downloaded.",
            "Crawl a site safely" => "Steps:\n1) Open Site Crawl.\n2) Paste root URL.\n3) Set crawl limits (same domain/subpath, depth, max pages, workers).\n4) Click Crawl Site.\n5) Review staged results and select items.\n6) Add selected items to queue or start now.\n\nSafety controls:\n- Domain and scope constraints.\n- Deduplication and probe options.\n\nExpected result:\n- Crawl summary reports discovered/staged counts.\n- Selected assets flow into queue as normal download jobs.",
            "Use queue manager" => "Primary controls:\n- Start/Pause/Stop Queue\n- Start/Pause/Resume/Retry/Remove Selected\n- Retry Failed, Clear Completed, Clear Failed\n- Move selected Top/Up/Down/Bottom\n- Set selected priority High/Normal/Low\n\nInspector shows:\n- Source URL, resolved URL, output, status message, error\n- Quick actions: open source, open folder, copy URL\n\nExpected result:\n- Queue remains manageable for batch workflows.\n- Ordering and priority changes affect execution order.",
            "Queue states and what they mean" => "State meanings:\n- Staged: collected but not queued for execution yet.\n- Queued: waiting for execution.\n- Probing: metadata/engine probe in progress.\n- Downloading: active transfer.\n- Processing: post-download step (merge/finalize).\n- Paused: stopped intentionally and resumable via queue actions.\n- Completed: successful finish.\n- Failed: retries exhausted or terminal error.\n- Cancelled: cancelled by user/system action.\n- Skipped: intentionally not written (e.g. duplicate policy).\n\nExpected result:\n- Status values in queue map directly to real lifecycle transitions.",
            "Use history and redownload" => "Steps:\n1) Open History & Diagnostics.\n2) Select a finished or failed item.\n3) Use Open File / Open Folder / Open Source URL / Copy URL.\n4) Use Redownload Selected to enqueue same source again.\n5) Use Clear History when you want a clean history store.\n\nExpected result:\n- History provides auditability and quick re-run workflows.",
            "Use categories and rules" => "Where:\n- Settings -> Rules tab.\n\nWhat you can define:\n- Rule name\n- Default folder\n- Extension patterns\n- Domain match patterns\n- Priority override\n\nUsage:\n1) Add or edit rules.\n2) Save Settings.\n3) New incoming jobs are classified by extension/domain.\n\nExpected result:\n- Category and destination are auto-assigned without manual per-job edits.",
            "Scheduler basics" => "Capabilities:\n- One-time schedule start.\n- One-time schedule pause.\n\nSteps:\n1) Go to Settings tab.\n2) Set date/time for start and pause.\n3) Apply Schedule Start / Schedule Pause.\n4) Check scheduler status text.\n5) Use Clear Schedule to remove pending actions.\n\nExpected result:\n- Queue starts/pauses at planned times without manual interaction.",
            "Diagnostics and logging" => "Diagnostics surfaces:\n- Live recent events panel in History & Diagnostics.\n- Export Diagnostics action for external troubleshooting.\n- Optional log-level settings in downloader settings model.\n\nHow to use:\n1) Reproduce issue.\n2) Inspect recent events for failure context.\n3) Export diagnostics file.\n4) Share diagnostics with issue details.\n\nExpected result:\n- Failures are observable and actionable instead of silent.",
            "Troubleshooting common issues" => "1) 'Media downloaded as MP3 unexpectedly'\n- Ensure Media tab Output = Video.\n- Analyze Media should show video plan before start.\n\n2) 'Asset/Crawl URL downloaded as random .bin'\n- Use Asset Grabber or Site Crawl actions (Scan Page / Crawl Site).\n- Discovery modes stage first; they are not direct download actions.\n\n3) 'Nothing starts'\n- Check queue state (Staged vs Queued).\n- Click Start Queue or Start Selected.\n\n4) 'Media tools missing'\n- Install Tools from header.\n- Retry media workflow.\n\n5) 'File already exists'\n- Check duplicate handling mode in settings (Skip/AutoRename/Overwrite).",
            "Feature validation checklist" => "Quick smoke test sequence:\n\nA) Quick mode\n- Paste 2 URLs -> Add to Queue -> verify queue rows appear.\n\nB) YouTube quality tab\n- Paste YouTube URL -> choose quality profile -> Analyze YouTube -> Add.\n- Verify plan text reflects selected video/audio quality constraints.\n\nC) Media video default\n- Paste YouTube URL in Media tab with Output=Video -> Analyze -> Add.\n- Verify plan indicates video, not audio-only.\n\nD) Audio explicit\n- Switch Output=AudioOnly -> Analyze -> Add.\n- Verify plan indicates audio-only.\n\nE) Asset grabber\n- Scan one page -> select subset -> Add Selected to Queue.\n- Verify only selected items are added.\n\nF) Site crawl\n- Crawl with domain/depth limits -> staged results appear.\n\nG) Queue controls\n- Reorder selected up/down and change priority.\n- Verify execution order/status changes.\n\nH) History/diagnostics\n- Complete a job -> appears in history.\n- Export diagnostics file.\n\nIf all steps pass, core downloader workflows are operating correctly.",
            _ => topic,
        };
    }

    private bool FilterHelpTopic(object obj)
    {
        if (obj is not string topic)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(HelpSearchText))
        {
            return true;
        }

        var query = HelpSearchText.Trim();
        return topic.Contains(query, StringComparison.OrdinalIgnoreCase);
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
        normalized = ExtractNormalizedUrls(input).FirstOrDefault() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(normalized);
    }

    private static List<string> ExtractNormalizedUrls(string input)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return result;
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
                result.Add(uri.ToString());
            }
        }

        return result;
    }

    private static string FormatIgnoredMessage(int ignoredCount)
    {
        return ignoredCount <= 0 ? string.Empty : $", ignored {ignoredCount} non-YouTube link(s)";
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
