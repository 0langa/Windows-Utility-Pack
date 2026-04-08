using System.Windows;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.Storage;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.Tools.SystemUtilities.StorageMaster;
using WindowsUtilityPack.Tools.FileDataTools.BulkFileRenamer;
using WindowsUtilityPack.Tools.SecurityPrivacy.PasswordGenerator;
using WindowsUtilityPack.Tools.NetworkInternet.PingTool;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader;
using WindowsUtilityPack.Tools.DeveloperProductivity.RegexTester;
using WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Services.Downloader.Engines;
using WindowsUtilityPack.Services.QrCode;
using WindowsUtilityPack.Services.TextConversion;
using WindowsUtilityPack.Tools.DeveloperProductivity.QrCodeGenerator;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack;

/// <summary>
/// Application entry point and service host.
///
/// Startup sequence:
///   1. OnStartup initialises all singleton services.
///   2. The saved theme is applied.
///   3. RegisterTools populates the ToolRegistry and wires every tool key to the NavigationService.
///   4. WPF creates MainWindow and navigates to "home" on first load.
///
/// Services are exposed as static properties so ViewModels can access them.
/// The preferred pattern is constructor injection where possible.
/// </summary>
public partial class App : Application
{
    // Singleton service accessors

    public static IThemeService      ThemeService      { get; private set; } = null!;
    public static INavigationService NavigationService { get; private set; } = null!;
    public static ISettingsService   SettingsService   { get; private set; } = null!;
    public static ILoggingService    LoggingService    { get; private set; } = null!;
    public static INotificationService NotificationService { get; private set; } = null!;
    public static IFolderPickerService FolderPickerService { get; private set; } = null!;
    public static IUserDialogService   UserDialogService   { get; private set; } = null!;
    public static IClipboardService    ClipboardService    { get; private set; } = null!;

    // Text Format Converter services

    /// <summary>Presents file open/save dialogs for text conversion workflows.</summary>
    public static IFileDialogService FileDialogService { get; private set; } = null!;
    /// <summary>Provides the core text conversion and formatting pipeline.</summary>
    public static ITextFormatConversionService TextFormatConversionService { get; private set; } = null!;
    /// <summary>Builds rich preview documents for converted output.</summary>
    public static ITextPreviewDocumentBuilder TextPreviewDocumentBuilder { get; private set; } = null!;
    /// <summary>Handles saving conversion results to disk.</summary>
    public static ITextResultExportService TextResultExportService { get; private set; } = null!;
    /// <summary>Manages the modeless conversion preview window.</summary>
    public static ITextPreviewWindowService TextPreviewWindowService { get; private set; } = null!;

    // Storage Master services

    /// <summary>Core storage scan engine for Storage Master.</summary>
    public static IScanEngine                   ScanEngine                  { get; private set; } = null!;
    /// <summary>Duplicate file detection service.</summary>
    public static IDuplicateDetectionService    DuplicateDetectionService   { get; private set; } = null!;
    /// <summary>Cleanup recommendation analysis service.</summary>
    public static ICleanupRecommendationService CleanupRecommendationService { get; private set; } = null!;
    /// <summary>Snapshot persistence service.</summary>
    public static ISnapshotService              SnapshotService             { get; private set; } = null!;
    /// <summary>Report and export generation service.</summary>
    public static IReportService                ReportService               { get; private set; } = null!;
    /// <summary>Application-level elevation/admin mode service.</summary>
    public static IElevationService             ElevationService            { get; private set; } = null!;
    /// <summary>Drive analysis and media type detection service.</summary>
    public static IDriveAnalysisService         DriveAnalysisService        { get; private set; } = null!;

    // Downloader services

    /// <summary>Manages external tool dependencies (yt-dlp, gallery-dl, ffmpeg).</summary>
    public static IDependencyManagerService DependencyManagerService { get; private set; } = null!;
    /// <summary>Scrapes and extracts assets from web pages and crawl scopes.</summary>
    public static IWebScraperService WebScraperService { get; private set; } = null!;
    /// <summary>Loads and persists downloader settings.</summary>
    public static IDownloaderSettingsService DownloaderSettingsService { get; private set; } = null!;
    /// <summary>Parses and normalizes URL inputs.</summary>
    public static IDownloadInputParserService DownloadInputParserService { get; private set; } = null!;
    /// <summary>Resolves categories and default routing rules.</summary>
    public static IDownloadCategoryService DownloadCategoryService { get; private set; } = null!;
    /// <summary>Writes downloader diagnostics and event logs.</summary>
    public static IDownloadEventLogService DownloadEventLogService { get; private set; } = null!;
    /// <summary>Persists completed/failed downloader history.</summary>
    public static IDownloadHistoryService DownloadHistoryService { get; private set; } = null!;
    /// <summary>Schedules one-time queue start/pause actions.</summary>
    public static IDownloadSchedulerService DownloadSchedulerService { get; private set; } = null!;
    /// <summary>Discovers downloadable assets from pages and crawl scopes.</summary>
    public static IAssetDiscoveryService AssetDiscoveryService { get; private set; } = null!;
    /// <summary>Selects the best download engine per job.</summary>
    public static IDownloadEngineResolver DownloadEngineResolver { get; private set; } = null!;
    /// <summary>Coordinates queue lifecycle and job execution.</summary>
    public static IDownloadCoordinatorService DownloadCoordinatorService { get; private set; } = null!;
    /// <summary>Open/save dialogs for downloader workflows.</summary>
    public static IDownloaderFileDialogService DownloaderFileDialogService { get; private set; } = null!;

    // QR Code Generator services

    /// <summary>Core QR code generation and export service.</summary>
    public static IQrCodeService QrCodeService { get; private set; } = null!;
    /// <summary>Dialog service for QR logo/import/export file paths.</summary>
    public static IQrCodeFileDialogService QrCodeFileDialogService { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialise core services
        LoggingService      = new LoggingService();
        SettingsService     = new SettingsService();
       NavigationService   = new NavigationService();
        ThemeService        = new ThemeService();
        NotificationService = new NotificationService();
        FolderPickerService = new FolderPickerService();
        UserDialogService   = new UserDialogService();
        ClipboardService    = new ClipboardService();
        FileDialogService   = new FileDialogService();
        TextFormatConversionService = new TextFormatConversionService();
        TextPreviewDocumentBuilder = new TextPreviewDocumentBuilder();
        TextResultExportService = new TextResultExportService(FileDialogService);
        TextPreviewWindowService = new TextPreviewWindowService();

        // Initialise Storage Master services
        ScanEngine                   = new ScanEngine();
        DuplicateDetectionService    = new DuplicateDetectionService();
        CleanupRecommendationService = new CleanupRecommendationService();
        SnapshotService              = new SnapshotService();
        ReportService                = new ReportService();
        ElevationService             = new ElevationService();
        DriveAnalysisService         = new DriveAnalysisService();

        // Initialise Downloader services
        DependencyManagerService = new DependencyManagerService();
        WebScraperService = new WebScraperService(DependencyManagerService);
        DownloaderSettingsService = new DownloaderSettingsService(SettingsService);
        DownloadInputParserService = new DownloadInputParserService();
        DownloadCategoryService = new DownloadCategoryService();
        DownloadSchedulerService = new DownloadSchedulerService();
        DownloadHistoryService = new DownloadHistoryService();
        DownloadEventLogService = new DownloadEventLogService(() => DownloaderSettingsService.Load());
        DownloaderFileDialogService = new DownloaderFileDialogService();

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

        // Initialise QR services
        QrCodeService = new QrCodeService();
        QrCodeFileDialogService = new QrCodeFileDialogService();

        var settings = SettingsService.Load();
        ThemeService.SetTheme(settings.Theme);

        RegisterTools();
        LoggingService.LogInfo("Application started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LoggingService.LogInfo("Application exiting.");
        base.OnExit(e);
    }

    /// <summary>
    /// Registers every available tool with the ToolRegistry.
    ///
    /// To add a new tool:
    ///   1. Create the ViewModel + View pair under Tools/Category/ToolName/.
    ///   2. Add a ToolDefinition block here.
    ///   3. Add the matching DataTemplate in App.xaml.
    ///   4. Optionally add a MenuEntry to MainWindow.xaml.
    /// </summary>
    private static void RegisterTools()
    {
        // ── Category icon registration (Segoe MDL2 Assets glyphs) ─────────
        ToolRegistry.RegisterCategoryIcon("System Utilities",       "\uE770");
        ToolRegistry.RegisterCategoryIcon("File & Data Tools",      "\uE8B7");
        ToolRegistry.RegisterCategoryIcon("Security & Privacy",     "\uE72E");
        ToolRegistry.RegisterCategoryIcon("Network & Internet",     "\uE774");
        ToolRegistry.RegisterCategoryIcon("Developer & Productivity", "\uE943");

        // ── Tool registration ─────────────────────────────────────────────
        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "home",
            Name        = "Home",
            Category    = "General",
            Icon        = "\U0001F3E0",
            IconGlyph   = "\uE80F",
            Description = "Application dashboard",
            Factory     = () => new HomeViewModel(),
        });

        // Storage Master replaces the old Disk Info tool
        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "storage-master",
            Name        = "Storage Master",
            Category    = "System Utilities",
            Icon        = "\U0001F4BD",
            IconGlyph   = "\uEDA2",
            Description = "Advanced storage analysis, cleanup, and optimization",
            Factory     = () => new StorageMasterViewModel(
                ScanEngine,
                DuplicateDetectionService,
                CleanupRecommendationService,
                SnapshotService,
                ReportService,
                ElevationService,
                DriveAnalysisService,
                FolderPickerService,
                UserDialogService,
                ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "bulk-renamer",
            Name        = "Bulk File Renamer",
            Category    = "File & Data Tools",
            Icon        = "\U0001F4C1",
            IconGlyph   = "\uE8AC",
            Description = "Rename multiple files with prefix, suffix, or find-replace rules",
            Factory     = () => new BulkFileRenamerViewModel(FolderPickerService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "password-generator",
            Name        = "Password Generator",
            Category    = "Security & Privacy",
            Icon        = "\U0001F512",
            IconGlyph   = "\uE8D7",
            Description = "Generate secure random passwords instantly",
            Factory     = () => new PasswordGeneratorViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "ping-tool",
            Name        = "Ping Tool",
            Category    = "Network & Internet",
            Icon        = "\U0001F310",
            IconGlyph   = "\uE968",
            Description = "Test network connectivity and measure latency",
            Factory     = () => new PingToolViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "regex-tester",
            Name        = "Regex Tester",
            Category    = "Developer & Productivity",
            Icon        = "\U0001F4BB",
            IconGlyph   = "\uE8FD",
            Description = "Test and debug regular expressions interactively",
            Factory     = () => new RegexTesterViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "downloader",
            Name        = "Downloader Studio",
            Category    = "Network & Internet",
            Icon        = "\U0001F4E5",
            IconGlyph   = "\uE896",
            Description = "Premium queue manager, media downloader, and asset extraction workspace",
            Factory     = () => new DownloaderViewModel(
                DownloadCoordinatorService,
                AssetDiscoveryService,
                DownloaderSettingsService,
                DependencyManagerService,
                DownloadEventLogService,
                DownloadSchedulerService,
                DownloaderFileDialogService,
                ClipboardService,
                UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "text-format-converter",
            Name        = "Text Format Converter",
            Category    = "Developer & Productivity",
            Icon        = "\U0001F9FE",
            IconGlyph   = "\uE8C1",
            Description = "Convert, format, and preview text across multiple formats",
            Factory     = () => new TextFormatConverterViewModel(
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
            Key         = "qr-code-generator",
            Name        = "QR Code Generator",
            Category    = "Developer & Productivity",
            Icon        = "\U0001F4F1",
            IconGlyph   = "\uED14",
            Description = "Generate, style, and export QR codes for URLs",
            Factory     = () => new QrCodeGeneratorViewModel(
                QrCodeService,
                QrCodeFileDialogService,
                ClipboardService,
                UserDialogService,
                SettingsService),
        });

        ToolRegistry.RegisterAll(NavigationService);
    }
}
