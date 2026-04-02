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
using WindowsUtilityPack.Services.TextConversion;
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
        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "home",
            Name        = "Home",
            Category    = "General",
            Icon        = "\U0001F3E0",
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
            Description = "Rename multiple files with prefix/suffix/find-replace",
            Factory     = () => new BulkFileRenamerViewModel(FolderPickerService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "password-generator",
            Name        = "Password Generator",
            Category    = "Security & Privacy",
            Icon        = "\U0001F512",
            Description = "Generate secure random passwords",
            Factory     = () => new PasswordGeneratorViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "ping-tool",
            Name        = "Ping Tool",
            Category    = "Network & Internet",
            Icon        = "\U0001F310",
            Description = "Ping hosts and measure network latency",
            Factory     = () => new PingToolViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "regex-tester",
            Name        = "Regex Tester",
            Category    = "Developer & Productivity",
            Icon        = "\U0001F4BB",
            Description = "Test regular expressions against input text",
            Factory     = () => new RegexTesterViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "downloader",
            Name        = "Downloader",
            Category    = "Network & Internet",
            Icon        = "\U0001F4E5",
            Description = "Download files from the web with progress tracking",
            Factory     = () => new DownloaderViewModel(FolderPickerService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "text-format-converter",
            Name        = "Text Format Converter & Formatter",
            Category    = "Developer & Productivity",
            Icon        = "\U0001F9FE",
            Description = "Convert, format, and preview HTML, XML, Markdown, RTF, PDF, DOCX, and JSON.",
            Factory     = () => new TextFormatConverterViewModel(
                ClipboardService,
                FileDialogService,
                TextFormatConversionService,
                TextPreviewDocumentBuilder,
                TextPreviewWindowService,
                TextResultExportService,
                UserDialogService),
        });

        ToolRegistry.RegisterAll(NavigationService);
    }
}
