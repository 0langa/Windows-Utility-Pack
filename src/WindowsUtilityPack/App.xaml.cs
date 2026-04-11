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
using WindowsUtilityPack.Tools.DeveloperProductivity.Base64Encoder;
using WindowsUtilityPack.Tools.DeveloperProductivity.ApiMockServer;
using WindowsUtilityPack.Tools.DeveloperProductivity.ClipboardManager;
using WindowsUtilityPack.Tools.DeveloperProductivity.ColorPicker;
using WindowsUtilityPack.Tools.DeveloperProductivity.DiffTool;
using WindowsUtilityPack.Tools.DeveloperProductivity.JsonYamlValidator;
using WindowsUtilityPack.Tools.DeveloperProductivity.LogFileAnalyzer;
using WindowsUtilityPack.Tools.DeveloperProductivity.MarkdownEditor;
using WindowsUtilityPack.Tools.DeveloperProductivity.QrCodeGenerator;
using WindowsUtilityPack.Tools.DeveloperProductivity.RegexTester;
using WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;
using WindowsUtilityPack.Tools.DeveloperProductivity.TimestampConverter;
using WindowsUtilityPack.Tools.DeveloperProductivity.UuidGenerator;
using WindowsUtilityPack.Tools.FileDataTools.BulkFileRenamer;
using WindowsUtilityPack.Tools.FileDataTools.FileHashCalculator;
using WindowsUtilityPack.Tools.FileDataTools.FileSplitterJoiner;
using WindowsUtilityPack.Tools.FileDataTools.MetadataEditor;
using WindowsUtilityPack.Tools.FileDataTools.SecureFileShredder;
using WindowsUtilityPack.Tools.ImageTools.ImageFormatConverter;
using WindowsUtilityPack.Tools.ImageTools.ImageResizer;
using WindowsUtilityPack.Tools.ImageTools.ScreenshotAnnotator;
using WindowsUtilityPack.Tools.NetworkInternet.DnsLookup;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.HttpRequestTester;
using WindowsUtilityPack.Tools.NetworkInternet.NetworkSpeedTest;
using WindowsUtilityPack.Tools.NetworkInternet.PingTool;
using WindowsUtilityPack.Tools.NetworkInternet.PortScanner;
using WindowsUtilityPack.Tools.NetworkInternet.SshRemoteTool;
using WindowsUtilityPack.Tools.SecurityPrivacy.CertificateInspector;
using WindowsUtilityPack.Tools.SecurityPrivacy.CertificateManager;
using WindowsUtilityPack.Tools.SecurityPrivacy.HashGenerator;
using WindowsUtilityPack.Tools.SecurityPrivacy.LocalSecretVault;
using WindowsUtilityPack.Tools.SecurityPrivacy.PasswordGenerator;
using WindowsUtilityPack.Tools.SystemUtilities.EnvVarsEditor;
using WindowsUtilityPack.Tools.SystemUtilities.HostsFileEditor;
using WindowsUtilityPack.Tools.SystemUtilities.ActivityLog;
using WindowsUtilityPack.Tools.SystemUtilities.BackgroundTaskMonitor;
using WindowsUtilityPack.Tools.SystemUtilities.AutomationRules;
using WindowsUtilityPack.Tools.SystemUtilities.EventLogViewer;
using WindowsUtilityPack.Tools.SystemUtilities.HotkeyManager;
using WindowsUtilityPack.Tools.SystemUtilities.ProcessExplorer;
using WindowsUtilityPack.Tools.SystemUtilities.RegistryEditor;
using WindowsUtilityPack.Tools.SystemUtilities.StartupManager;
using WindowsUtilityPack.Tools.SystemUtilities.TaskSchedulerUi;
using WindowsUtilityPack.Tools.SystemUtilities.StorageMaster;
using WindowsUtilityPack.Tools.SystemUtilities.SystemInfoDashboard;
using WindowsUtilityPack.Tools.SystemUtilities.WorkspaceProfiles;
using WindowsUtilityPack.ViewModels;

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
        UlidGenerator = new UlidGenerator();
        LogFileAnalyzerService = new LogFileAnalyzerService();
        MarkdownEditorService = new MarkdownEditorService();
        ApiMockServerService = new ApiMockServerService();

        HomeDashboardService = new HomeDashboardService(SettingsService);

        var settings = SettingsService.Load();
        ThemeService.SetTheme(settings.Theme);

        RegisterTools();

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

    private static void RegisterTools()
    {
        ToolRegistry.RegisterCategoryIcon("System Utilities", "\uE770");
        ToolRegistry.RegisterCategoryIcon("File & Data Tools", "\uE8B7");
        ToolRegistry.RegisterCategoryIcon("Security & Privacy", "\uE72E");
        ToolRegistry.RegisterCategoryIcon("Network & Internet", "\uE774");
        ToolRegistry.RegisterCategoryIcon("Developer & Productivity", "\uE943");
        ToolRegistry.RegisterCategoryIcon("Image Tools", "\uEB9F");

        ToolRegistry.RegisterCategoryDescription("System Utilities", "Manage startup, environment, storage, and system info");
        ToolRegistry.RegisterCategoryDescription("File & Data Tools", "Rename, hash, shred, split, and inspect files");
        ToolRegistry.RegisterCategoryDescription("Security & Privacy", "Passwords, hashes, secrets, and certificates");
        ToolRegistry.RegisterCategoryDescription("Network & Internet", "Ping, DNS, ports, HTTP, speed, and downloads");
        ToolRegistry.RegisterCategoryDescription("Developer & Productivity", "Regex, encoding, colour, QR, diff, and more");
        ToolRegistry.RegisterCategoryDescription("Image Tools", "Resize, convert, and annotate images");

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "home",
            Name = "Home",
            Category = "General",
            Icon = "🏠",
            IconGlyph = "\uE80F",
            Description = "Application dashboard",
            Factory = () => new HomeViewModel(
                NavigationService,
                HomeDashboardService,
                SettingsService,
                ClipboardService,
                UserDialogService),
            
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "storage-master",
            Name = "Storage Master",
            Category = "System Utilities",
            Icon = "💽",
            IconGlyph = "\uEDA2",
            Description = "Advanced storage analysis, cleanup, and optimization",
            Factory = () => new StorageMasterViewModel(
                ScanEngine,
                DuplicateDetectionService,
                CleanupRecommendationService,
                CleanupAutomationPolicyService,
                SnapshotService,
                ReportService,
                ElevationService,
                DriveAnalysisService,
                FolderPickerService,
                UserDialogService,
                ClipboardService,
                BackgroundTaskService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "startup-manager",
            Name = "Startup Manager",
            Category = "System Utilities",
            IconGlyph = "\uE7B7",
            Description = "Manage startup entries from user and machine locations",
            Factory = () => new StartupManagerViewModel(ClipboardService, StartupDiagnosticsService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "system-info-dashboard",
            Name = "System Info Dashboard",
            Category = "System Utilities",
            IconGlyph = "\uE946",
            Description = "View hardware, OS, runtime and drive summaries",
            Factory = () => new SystemInfoViewModel(ClipboardService, SystemInfoReportService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "activity-log",
            Name = "Activity Log",
            Category = "System Utilities",
            IconGlyph = "\uE823",
            Description = "Review, filter, and export internal application activity events",
            Factory = () => new ActivityLogViewModel(ActivityLogService, ClipboardService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "background-task-monitor",
            Name = "Background Task Monitor",
            Category = "System Utilities",
            IconGlyph = "\uE945",
            Description = "Monitor and cancel long-running background tasks",
            Factory = () => new BackgroundTaskMonitorViewModel(BackgroundTaskService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "event-log-viewer",
            Name = "Event Log Viewer",
            Category = "System Utilities",
            IconGlyph = "\uE9D9",
            Description = "Filter and review Windows event logs by source, level, and event ID",
            Factory = () => new EventLogViewerViewModel(WindowsEventLogService, ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "hotkey-manager",
            Name = "Hotkey Manager",
            Category = "System Utilities",
            IconGlyph = "\uE765",
            Description = "Configure keyboard shortcuts and detect hotkey collisions",
            Factory = () => new HotkeyManagerViewModel(HotkeyService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "automation-rules",
            Name = "Automation Rules",
            Category = "System Utilities",
            IconGlyph = "\uE7BE",
            Description = "Define and evaluate local If-X-Then-notify automation rules",
            Factory = () => new AutomationRulesViewModel(AutomationRuleService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "process-explorer",
            Name = "Process Explorer",
            Category = "System Utilities",
            IconGlyph = "\uE7F8",
            Description = "Inspect running processes, filter by name/path, and terminate selected entries",
            Factory = () => new ProcessExplorerViewModel(ProcessExplorerService, UserDialogService, ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "registry-editor",
            Name = "Registry Editor",
            Category = "System Utilities",
            IconGlyph = "\uE943",
            Description = "Safely inspect and edit HKCU\\Software keys with backup and restore",
            Factory = () => new RegistryEditorViewModel(RegistryEditorService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "task-scheduler-ui",
            Name = "Task Scheduler UI",
            Category = "System Utilities",
            IconGlyph = "\uE823",
            Description = "Browse scheduled tasks and run selected tasks on demand",
            Factory = () => new TaskSchedulerUiViewModel(TaskSchedulerService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "workspace-profiles",
            Name = "Workspace Profiles",
            Category = "System Utilities",
            IconGlyph = "\uE7C1",
            Description = "Save and apply reusable startup and favorites workspace profiles",
            Factory = () => new WorkspaceProfilesViewModel(WorkspaceProfileCoordinator, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "env-vars-editor",
            Name = "Environment Variables Editor",
            Category = "System Utilities",
            IconGlyph = "\uE944",
            Description = "Inspect and edit user/system environment variables",
            Factory = () => new EnvVarsEditorViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "hosts-file-editor",
            Name = "Hosts File Editor",
            Category = "System Utilities",
            IconGlyph = "\uE774",
            Description = "Safely edit hosts file with backup and restore support",
            Factory = () => new HostsFileEditorViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "bulk-renamer",
            Name = "Bulk File Renamer",
            Category = "File & Data Tools",
            IconGlyph = "\uE8AC",
            Description = "Rename multiple files with prefix, suffix, or find-replace rules",
            Factory = () => new BulkFileRenamerViewModel(FolderPickerService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "file-hash-calculator",
            Name = "File Hash Calculator",
            Category = "File & Data Tools",
            IconGlyph = "\uE9D9",
            Description = "Compute and verify MD5, SHA-256, and SHA-512 file hashes",
            Keywords = ["checksum", "sha", "md5", "verify"],
            Factory = () => new FileHashCalculatorViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "secure-file-shredder",
            Name = "Secure File Shredder",
            Category = "File & Data Tools",
            IconGlyph = "\uE74D",
            Description = "Securely overwrite and delete files",
            Factory = () => new SecureFileShredderViewModel(UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "metadata-editor",
            Name = "Metadata Viewer/Editor",
            Category = "File & Data Tools",
            IconGlyph = "\uE7C3",
            Description = "Inspect and strip metadata from image and audio files",
            Factory = () => new MetadataEditorViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "file-splitter-joiner",
            Name = "File Splitter / Joiner",
            Category = "File & Data Tools",
            IconGlyph = "\uE8AB",
            Description = "Split large files into chunks and rejoin with checksum validation",
            Factory = () => new FileSplitterJoinerViewModel(FileSplitJoinService, FolderPickerService, ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "password-generator",
            Name = "Password Generator",
            Category = "Security & Privacy",
            IconGlyph = "\uE8D7",
            Description = "Generate secure random passwords instantly",
            Factory = () => new PasswordGeneratorViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "hash-generator",
            Name = "Hash Generator",
            Category = "Security & Privacy",
            IconGlyph = "\uE943",
            Description = "Generate and verify hashes for text and files",
            Factory = () => new HashGeneratorViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "local-secret-vault",
            Name = "Local Secret Vault",
            Category = "Security & Privacy",
            IconGlyph = "\uE72E",
            Description = "AES-256 encrypted local vault for credentials and notes",
            Factory = () => new LocalSecretVaultViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "certificate-inspector",
            Name = "Certificate Inspector",
            Category = "Security & Privacy",
            IconGlyph = "\uEB95",
            Description = "Inspect certificates from URL, file, or pasted PEM",
            Factory = () => new CertificateInspectorViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "certificate-manager",
            Name = "Certificate Manager",
            Category = "Security & Privacy",
            IconGlyph = "\uEB95",
            Description = "Browse certificate stores and copy certificate details or PEM content",
            Factory = () => new CertificateManagerViewModel(CertificateManagerService, ClipboardService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "ping-tool",
            Name = "Ping Tool",
            Category = "Network & Internet",
            IconGlyph = "\uE968",
            Description = "Test network connectivity and measure latency",
            Factory = () => new PingToolViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "dns-lookup",
            Name = "DNS Lookup",
            Category = "Network & Internet",
            IconGlyph = "\uE774",
            Description = "Query A, AAAA, CNAME, MX and TXT DNS records",
            Keywords = ["domain", "resolver", "records", "a", "aaaa", "mx", "txt"],
            Factory = () => new DnsLookupViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "port-scanner",
            Name = "Port Scanner",
            Category = "Network & Internet",
            IconGlyph = "\uEC27",
            Description = "Scan local or remote ports with async cancellation",
            Factory = () => new PortScannerViewModel(ClipboardService, BackgroundTaskService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "http-request-tester",
            Name = "HTTP Request Tester",
            Category = "Network & Internet",
            IconGlyph = "\uE774",
            Description = "Send custom HTTP requests and inspect responses",
            Factory = () => new HttpRequestTesterViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "network-speed-test",
            Name = "Network Speed Test",
            Category = "Network & Internet",
            IconGlyph = "\uE9D9",
            Description = "Run download/upload/latency speed checks",
            Factory = () => new NetworkSpeedTestViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "ssh-remote-tool",
            Name = "SSH Remote Tool",
            Category = "Network & Internet",
            IconGlyph = "\uE945",
            Description = "Manage SSH profiles, test host connectivity, and generate SSH commands",
            Factory = () => new SshRemoteToolViewModel(SshRemoteToolService, UserDialogService, ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "downloader",
            Name = "Downloader Studio",
            Category = "Network & Internet",
            IconGlyph = "\uE896",
            Description = "Premium queue manager, media downloader, and asset extraction workspace",
            Factory = () => new DownloaderViewModel(
                DownloadCoordinatorService,
                AssetDiscoveryService,
                DownloaderSettingsService,
                DependencyManagerService,
                DownloadEventLogService,
                DownloadSchedulerService,
                DownloaderFileDialogService,
                ClipboardService,
                UserDialogService,
                NavigationService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "regex-tester",
            Name = "Regex Tester",
            Category = "Developer & Productivity",
            IconGlyph = "\uE8FD",
            Description = "Test and debug regular expressions interactively",
            Factory = () => new RegexTesterViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "text-format-converter",
            Name = "Text Format Converter",
            Category = "Developer & Productivity",
            IconGlyph = "\uE8C1",
            Description = "Convert, format, and preview text across multiple formats",
            Factory = () => new TextFormatConverterViewModel(
                ClipboardService,
                FileDialogService,
                TextFormatConversionService,
                TextPreviewDocumentBuilder,
                TextPreviewWindowService,
                TextResultExportService,
                UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "log-file-analyzer",
            Name = "Log File Analyzer",
            Category = "Developer & Productivity",
            IconGlyph = "\uE9D2",
            Description = "Parse and filter text logs with severity summaries and quick triage",
            Factory = () => new LogFileAnalyzerViewModel(LogFileAnalyzerService, ClipboardService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "markdown-editor",
            Name = "Markdown Editor",
            Category = "Developer & Productivity",
            IconGlyph = "\uF000",
            Description = "Edit markdown files with live HTML rendering and document stats",
            Factory = () => new MarkdownEditorViewModel(MarkdownEditorService, ClipboardService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "api-mock-server",
            Name = "API Mock Server",
            Category = "Developer & Productivity",
            IconGlyph = "\uE774",
            Description = "Host local mock HTTP endpoints for integration and frontend testing",
            Factory = () => new ApiMockServerViewModel(ApiMockServerService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "qr-code-generator",
            Name = "QR Code Generator",
            Category = "Developer & Productivity",
            IconGlyph = "\uED14",
            Description = "Generate, style, and export QR codes for URLs",
            Keywords = ["qrcode", "barcode", "url", "share"],
            DateAdded = new DateTime(2026, 3, 28),
            Factory = () => new QrCodeGeneratorViewModel(
                QrCodeService,
                QrCodeFileDialogService,
                ClipboardService,
                UserDialogService,
                SettingsService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "clipboard-manager",
            Name = "Clipboard Manager",
            Category = "Developer & Productivity",
            IconGlyph = "\uE8C8",
            Description = "Persistent clipboard history with quick reuse and search",
            Factory = () => new ClipboardManagerViewModel(ClipboardService, ClipboardHistoryService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "color-picker",
            Name = "Color Picker",
            Category = "Developer & Productivity",
            IconGlyph = "\uEC7A",
            Description = "Pick screen colors and build reusable palettes",
            Factory = () => new ColorPickerViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "timestamp-converter",
            Name = "Timestamp Converter",
            Category = "Developer & Productivity",
            IconGlyph = "\uE823",
            Description = "Convert Unix epochs and human-readable time values",
            Factory = () => new TimestampConverterViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "uuid-generator",
            Name = "UUID / ULID Generator",
            Category = "Developer & Productivity",
            IconGlyph = "\uE9CE",
            Description = "Generate UUIDs and ULIDs with bulk copy support",
            Keywords = ["guid", "identifier", "ids", "ulid"],
            Factory = () => new UuidGeneratorViewModel(ClipboardService, UlidGenerator),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "base64-url-encoder",
            Name = "Base64 / URL Encoder-Decoder",
            Category = "Developer & Productivity",
            IconGlyph = "\uE943",
            Description = "Encode and decode Base64, URL, and HTML text",
            Factory = () => new Base64EncoderViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "diff-tool",
            Name = "Diff Tool",
            Category = "Developer & Productivity",
            IconGlyph = "\uE73E",
            Description = "Compare text side-by-side with line-level changes",
            Factory = () => new DiffToolViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "json-yaml-validator",
            Name = "JSON / YAML Validator",
            Category = "Developer & Productivity",
            IconGlyph = "\uE943",
            Description = "Validate and prettify JSON and YAML payloads",
            Keywords = ["schema", "lint", "format", "prettify", "parser"],
            DateAdded = new DateTime(2026, 3, 30),
            Factory = () => new JsonYamlValidatorViewModel(StructuredDataValidationService, ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "image-resizer",
            Name = "Image Resizer & Compressor",
            Category = "Image Tools",
            IconGlyph = "\uE91B",
            Description = "Batch resize and compress JPG, PNG, WEBP, BMP and TIFF",
            Keywords = ["resize", "compress", "optimize", "jpg", "png", "webp"],
            DateAdded = new DateTime(2026, 3, 25),
            Factory = () => new ImageResizerViewModel(ImageProcessingService, FolderPickerService, ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "image-format-converter",
            Name = "Image Format Converter",
            Category = "Image Tools",
            IconGlyph = "\uEC17",
            Description = "Batch convert image formats with quality controls",
            Keywords = ["convert", "jpg", "png", "webp", "bmp", "tiff"],
            DateAdded = new DateTime(2026, 3, 25),
            Factory = () => new ImageFormatConverterViewModel(ImageProcessingService, FolderPickerService, ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "screenshot-annotator",
            Name = "Screenshot Annotator",
            Category = "Image Tools",
            IconGlyph = "\uE722",
            Description = "Capture screenshots and apply rectangles, arrows, text, blur, or redaction",
            Keywords = ["capture", "markup", "redact", "blur", "annotate"],
            DateAdded = new DateTime(2026, 3, 25),
            Factory = () => new ScreenshotAnnotatorViewModel(ImageProcessingService, ClipboardService),
        });

        ToolRegistry.RegisterAll(NavigationService);
    }
}
