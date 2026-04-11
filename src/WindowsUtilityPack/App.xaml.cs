using System.Windows;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Services.Downloader.Engines;
using WindowsUtilityPack.Services.FileTools;
using WindowsUtilityPack.Services.Identifier;
using WindowsUtilityPack.Services.ImageTools;
using WindowsUtilityPack.Services.QrCode;
using WindowsUtilityPack.Services.Storage;
using WindowsUtilityPack.Services.StructuredData;
using WindowsUtilityPack.Services.TextConversion;
using WindowsUtilityPack.Tools;

namespace WindowsUtilityPack;

/// <summary>
/// Application entry point and service host.
/// </summary>
public partial class App : Application
{
    public static IThemeService ThemeService { get; private set; } = null!;
    public static INavigationService NavigationService { get; private set; } = null!;
    public static ISettingsService SettingsService { get; private set; } = null!;
    public static ILoggingService LoggingService { get; private set; } = null!;
    public static INotificationService NotificationService { get; private set; } = null!;
    public static IFolderPickerService FolderPickerService { get; private set; } = null!;
    public static IUserDialogService UserDialogService { get; private set; } = null!;
    public static IClipboardService ClipboardService { get; private set; } = null!;
    public static IHomeDashboardService HomeDashboardService { get; private set; } = null!;
    public static SystemVitalsService VitalsService { get; private set; } = null!;
    public static IAppDataStoreService AppDataStoreService { get; private set; } = null!;
    public static IActivityLogService ActivityLogService { get; private set; } = null!;
    public static IWorkspaceProfileService WorkspaceProfileService { get; private set; } = null!;
    public static ICommandPaletteService CommandPaletteService { get; private set; } = null!;
    public static IBackgroundTaskService BackgroundTaskService { get; private set; } = null!;
    public static IClipboardHistoryService ClipboardHistoryService { get; private set; } = null!;
    public static IWorkspaceProfileCoordinator WorkspaceProfileCoordinator { get; private set; } = null!;
    public static IWindowsEventLogService WindowsEventLogService { get; private set; } = null!;
    public static IHotkeyService HotkeyService { get; private set; } = null!;
    public static IGlobalHotkeyService GlobalHotkeyService { get; private set; } = null!;
    public static IAutomationRuleService AutomationRuleService { get; private set; } = null!;
    public static IProcessExplorerService ProcessExplorerService { get; private set; } = null!;
    public static IRegistryEditorService RegistryEditorService { get; private set; } = null!;
    public static ITaskSchedulerService TaskSchedulerService { get; private set; } = null!;
    public static IToolWindowHostService ToolWindowHostService { get; private set; } = null!;
    public static ICleanupAutomationPolicyService CleanupAutomationPolicyService { get; private set; } = null!;
    public static IStartupDiagnosticsService StartupDiagnosticsService { get; private set; } = null!;
    public static ISystemInfoReportService SystemInfoReportService { get; private set; } = null!;

    private readonly SemaphoreSlim _automationEvalGate = new(1, 1);

    public static IFileDialogService FileDialogService { get; private set; } = null!;
    public static ITextFormatConversionService TextFormatConversionService { get; private set; } = null!;
    public static ITextPreviewDocumentBuilder TextPreviewDocumentBuilder { get; private set; } = null!;
    public static ITextResultExportService TextResultExportService { get; private set; } = null!;
    public static ITextPreviewWindowService TextPreviewWindowService { get; private set; } = null!;

    public static IScanEngine ScanEngine { get; private set; } = null!;
    public static IDuplicateDetectionService DuplicateDetectionService { get; private set; } = null!;
    public static ICleanupRecommendationService CleanupRecommendationService { get; private set; } = null!;
    public static ISnapshotService SnapshotService { get; private set; } = null!;
    public static IReportService ReportService { get; private set; } = null!;
    public static IElevationService ElevationService { get; private set; } = null!;
    public static IDriveAnalysisService DriveAnalysisService { get; private set; } = null!;

    public static IDependencyManagerService DependencyManagerService { get; private set; } = null!;
    public static IWebScraperService WebScraperService { get; private set; } = null!;
    public static IDownloaderSettingsService DownloaderSettingsService { get; private set; } = null!;
    public static IDownloadInputParserService DownloadInputParserService { get; private set; } = null!;
    public static IDownloadCategoryService DownloadCategoryService { get; private set; } = null!;
    public static IDownloadEventLogService DownloadEventLogService { get; private set; } = null!;
    public static IDownloadHistoryService DownloadHistoryService { get; private set; } = null!;
    public static IDownloadSchedulerService DownloadSchedulerService { get; private set; } = null!;
    public static IAssetDiscoveryService AssetDiscoveryService { get; private set; } = null!;
    public static IDownloadEngineResolver DownloadEngineResolver { get; private set; } = null!;
    public static IDownloadCoordinatorService DownloadCoordinatorService { get; private set; } = null!;
    public static IDownloaderFileDialogService DownloaderFileDialogService { get; private set; } = null!;
    public static ISshRemoteToolService SshRemoteToolService { get; private set; } = null!;

    public static IQrCodeService QrCodeService { get; private set; } = null!;
    public static IQrCodeFileDialogService QrCodeFileDialogService { get; private set; } = null!;
    public static ICertificateManagerService CertificateManagerService { get; private set; } = null!;

    public static IStructuredDataValidationService StructuredDataValidationService { get; private set; } = null!;
    public static IFileSplitJoinService FileSplitJoinService { get; private set; } = null!;
    public static IImageProcessingService ImageProcessingService { get; private set; } = null!;
    public static IUlidGenerator UlidGenerator { get; private set; } = null!;
    public static ILogFileAnalyzerService LogFileAnalyzerService { get; private set; } = null!;
    public static IMarkdownEditorService MarkdownEditorService { get; private set; } = null!;
    public static IApiMockServerService ApiMockServerService { get; private set; } = null!;
    public static IQuickScreenshotService QuickScreenshotService { get; private set; } = null!;
    public static IQuickCaptureStateService QuickCaptureStateService { get; private set; } = null!;
    public static ITrayIconService TrayIconService { get; private set; } = null!;

    public static ITrayService TrayService { get; private set; } = null!;
    public static IGlobalHotkeyService GlobalHotkeyService { get; private set; } = null!;

    public static ISettingsService? TryGetSettingsService() => SettingsService;
    public static IThemeService? TryGetThemeService() => ThemeService;
    public static ILoggingService? TryGetLoggingService() => LoggingService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LoggingService = new LoggingService();
        SettingsService = new SettingsService();
        NavigationService = new NavigationService();
        ThemeService = new ThemeService();
        NotificationService = new NotificationService();
        FolderPickerService = new FolderPickerService();
        UserDialogService = new UserDialogService();
        ClipboardService = new ClipboardService();
        VitalsService = new SystemVitalsService();
        AppDataStoreService = new AppDataStoreService();
        ActivityLogService = new ActivityLogService(AppDataStoreService);
        WorkspaceProfileService = new WorkspaceProfileService(AppDataStoreService);
        WorkspaceProfileCoordinator = new WorkspaceProfileCoordinator(WorkspaceProfileService, SettingsService, ActivityLogService);
        CommandPaletteService = new CommandPaletteService();
        BackgroundTaskService = new BackgroundTaskService();
        ClipboardHistoryService = new ClipboardHistoryService(AppDataStoreService);
        WindowsEventLogService = new WindowsEventLogService();
        HotkeyService = new HotkeyService(SettingsService);
        GlobalHotkeyService = new GlobalHotkeyService(HotkeyService, LoggingService);
        AutomationRuleService = new AutomationRuleService(AppDataStoreService);
        ProcessExplorerService = new ProcessExplorerService();
        RegistryEditorService = new RegistryEditorService();
        TaskSchedulerService = new TaskSchedulerService();
        ToolWindowHostService = new ToolWindowHostService();
        CleanupAutomationPolicyService = new CleanupAutomationPolicyService();
        StartupDiagnosticsService = new StartupDiagnosticsService();
        SystemInfoReportService = new SystemInfoReportService();

        VitalsService.Updated += OnVitalsUpdatedForAutomation;

        FileDialogService = new FileDialogService();
        TextFormatConversionService = new TextFormatConversionService();
        TextPreviewDocumentBuilder = new TextPreviewDocumentBuilder();
        TextResultExportService = new TextResultExportService(FileDialogService);
        TextPreviewWindowService = new TextPreviewWindowService();

        ScanEngine = new ScanEngine();
        DuplicateDetectionService = new DuplicateDetectionService();
        CleanupRecommendationService = new CleanupRecommendationService();
        SnapshotService = new SnapshotService();
        ReportService = new ReportService();
        ElevationService = new ElevationService();
        DriveAnalysisService = new DriveAnalysisService();

        DependencyManagerService = new DependencyManagerService();
        WebScraperService = new WebScraperService(DependencyManagerService);
        DownloaderSettingsService = new DownloaderSettingsService(SettingsService);
        DownloadInputParserService = new DownloadInputParserService();
        DownloadCategoryService = new DownloadCategoryService();
        DownloadSchedulerService = new DownloadSchedulerService();
        DownloadHistoryService = new DownloadHistoryService();
        DownloadEventLogService = new DownloadEventLogService(() => DownloaderSettingsService.Load());
        DownloaderFileDialogService = new DownloaderFileDialogService();
        SshRemoteToolService = new SshRemoteToolService(AppDataStoreService);

        var directHttpEngine = new DirectHttpDownloadEngine();
        var mediaEngine = new MediaDownloadEngine(DependencyManagerService);
        var galleryEngine = new GalleryDownloadEngine(DependencyManagerService);
        var fallbackEngine = new FallbackDownloadEngine(directHttpEngine);

        DownloadEngineResolver = new DownloadEngineResolverService(
        [
            mediaEngine,
            galleryEngine,
            directHttpEngine,
            fallbackEngine,
        ]);

        AssetDiscoveryService = new AssetDiscoveryService(WebScraperService);
        DownloadCoordinatorService = new DownloadCoordinatorService(
            DownloadInputParserService,
            DownloadEngineResolver,
            DownloadCategoryService,
            DownloaderSettingsService,
            DownloadHistoryService,
            DownloadEventLogService,
            DownloadSchedulerService);

        QrCodeService = new QrCodeService();
        QrCodeFileDialogService = new QrCodeFileDialogService();
        CertificateManagerService = new CertificateManagerService();

        StructuredDataValidationService = new StructuredDataValidationService();
        FileSplitJoinService = new FileSplitJoinService();
        ImageProcessingService = new ImageProcessingService();
        QuickCaptureStateService = new QuickCaptureStateService();
        QuickScreenshotService = new QuickScreenshotService(ImageProcessingService, ClipboardService);
        TrayIconService = new TrayIconService();
        UlidGenerator = new UlidGenerator();
        LogFileAnalyzerService = new LogFileAnalyzerService();
        MarkdownEditorService = new MarkdownEditorService();
        ApiMockServerService = new ApiMockServerService();

        HomeDashboardService = new HomeDashboardService(SettingsService);

        TrayService = new TrayService();
        GlobalHotkeyService = new GlobalHotkeyService();

        var settings = SettingsService.Load();
        ThemeService.SetTheme(settings.Theme);

        RegisterTools();
        GlobalHotkeyService.Start();

        // Track tool launches for the Recently Used section.
        NavigationService.Navigated += (_, vmType) =>
        {
            var typeName = vmType.Name.Replace("ViewModel", "");
            foreach (var tool in ToolRegistry.All)
            {
                if (tool.Name.Replace(" ", "").Equals(typeName, StringComparison.OrdinalIgnoreCase))
                {
                    HomeDashboardService.RecordToolLaunch(tool.Key);
                    HomeDashboardService.IncrementLaunchCount(tool.Key);
                    _ = ActivityLogService.LogAsync("Navigation", "ToolOpened", tool.Key)
                        .ContinueWith(
                            static t =>
                            {
                                if (t.Exception is not null)
                                {
                                    App.TryGetLoggingService()?.LogError("Failed to write navigation activity event", t.Exception.Flatten());
                                }
                            },
                            TaskContinuationOptions.OnlyOnFaulted);
                    break;
                }
            }
        };

        LoggingService.LogInfo("Application started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LoggingService.LogInfo("Application exiting.");
        VitalsService.Updated -= OnVitalsUpdatedForAutomation;
        (ThemeService as IDisposable)?.Dispose();
        (NavigationService as IDisposable)?.Dispose();
        (DownloadCoordinatorService as IDisposable)?.Dispose();
        (GlobalHotkeyService as IDisposable)?.Dispose();
        (TrayIconService as IDisposable)?.Dispose();
        VitalsService.Dispose();
        base.OnExit(e);
    }

    private async void OnVitalsUpdatedForAutomation(object? sender, EventArgs e)
    {
        if (!await _automationEvalGate.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var alerts = await AutomationRuleService.EvaluateAsync(VitalsService).ConfigureAwait(true);
            foreach (var alert in alerts)
            {
                NotificationService.ShowInfo(alert.Message);
                _ = ActivityLogService.LogAsync("AutomationRules", "Triggered", alert.Rule.Name);
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Automation evaluation failed", ex);
        }
        finally
        {
            _automationEvalGate.Release();
        }
    }

    private static void RegisterTools() => ToolBootstrapper.RegisterTools();
}

