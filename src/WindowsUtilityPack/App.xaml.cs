using System.Windows;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.Tools.SystemUtilities.DiskInfo;
using WindowsUtilityPack.Tools.FileDataTools.BulkFileRenamer;
using WindowsUtilityPack.Tools.SecurityPrivacy.PasswordGenerator;
using WindowsUtilityPack.Tools.NetworkInternet.PingTool;
using WindowsUtilityPack.Tools.DeveloperProductivity.RegexTester;
using WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;
using WindowsUtilityPack.Services.TextConversion;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack;

/// <summary>
/// Application entry point and service host.
///
/// Startup sequence:
///   1. <see cref="OnStartup"/> initialises all singleton services.
///   2. The saved theme is applied (App.xaml already loaded DarkTheme.xaml;
///      ThemeService switches to the saved preference if it differs).
///   3. <see cref="RegisterTools"/> populates the <see cref="ToolRegistry"/>
///      and wires every tool key to the <see cref="NavigationService"/>.
///   4. WPF then creates <c>MainWindow</c> (via <c>StartupUri</c>) and
///      the window navigates to "home" on first load.
///
/// Services are exposed as static properties so that ViewModels that
/// cannot receive constructor injection (e.g. those created by WPF DataTemplates)
/// can still access them.  The preferred pattern is constructor injection where possible.
/// </summary>
public partial class App : Application
{
    // ── Singleton service accessors ───────────────────────────────────────────

    /// <summary>Manages dark/light theme switching.</summary>
    public static IThemeService ThemeService { get; private set; } = null!;

    /// <summary>Handles key-based navigation between tool ViewModels.</summary>
    public static INavigationService NavigationService { get; private set; } = null!;

    /// <summary>Loads and saves user preferences (theme, window geometry).</summary>
    public static ISettingsService SettingsService { get; private set; } = null!;

    /// <summary>Writes timestamped log entries to %LOCALAPPDATA%\WindowsUtilityPack\app.log.</summary>
    public static ILoggingService LoggingService { get; private set; } = null!;

    /// <summary>Raises events when in-app toast notifications should be shown.</summary>
    public static INotificationService NotificationService { get; private set; } = null!;

    /// <summary>Presents folder-picker dialogs to the user.</summary>
    public static IFolderPickerService FolderPickerService { get; private set; } = null!;

    /// <summary>Presents message boxes and confirmation dialogs to the user.</summary>
    public static IUserDialogService UserDialogService { get; private set; } = null!;

    /// <summary>Provides access to the system clipboard.</summary>
    public static IClipboardService ClipboardService { get; private set; } = null!;

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

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by WPF before the first window is shown.
    /// Initialises services, restores theme, and registers tools.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialise services in dependency order (logging first so everything else can log).
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

        // Restore the saved theme preference.
        // App.xaml already loads DarkTheme.xaml; ThemeService will swap to LightTheme if needed.
        var settings = SettingsService.Load();
        ThemeService.SetTheme(settings.Theme);

        // Populate ToolRegistry and register all tool keys with the NavigationService.
        RegisterTools();

        LoggingService.LogInfo("Application started.");
    }

    /// <summary>Called when the application is closing.  Log the exit event.</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        LoggingService.LogInfo("Application exiting.");
        base.OnExit(e);
    }

    // ── Tool registration ─────────────────────────────────────────────────────

    /// <summary>
    /// Registers every available tool with the <see cref="ToolRegistry"/> and
    /// subsequently calls <see cref="ToolRegistry.RegisterAll"/> to map each key
    /// to the <see cref="NavigationService"/>.
    ///
    /// To add a new tool:
    ///   1. Create the ViewModel + View pair under <c>Tools/&lt;Category&gt;/&lt;ToolName&gt;/</c>.
    ///   2. Add a <c>ToolDefinition</c> block here.
    ///   3. Add the matching <c>DataTemplate</c> in <c>App.xaml</c>.
    ///   4. Optionally add a <c>MenuEntry</c> to <c>MainWindow.xaml</c>.
    /// </summary>
    private static void RegisterTools()
    {
        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "home",
            Name        = "Home",
            Category    = "General",
            Icon        = "🏠",
            Description = "Application dashboard",
            Factory     = () => new HomeViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "disk-info",
            Name        = "Disk Info Viewer",
            Category    = "System Utilities",
            Icon        = "💾",
            Description = "View drive information and disk usage",
            Factory     = () => new DiskInfoViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "bulk-renamer",
            Name        = "Bulk File Renamer",
            Category    = "File & Data Tools",
            Icon        = "📁",
            Description = "Rename multiple files with prefix/suffix/find-replace",
            Factory     = () => new BulkFileRenamerViewModel(FolderPickerService, UserDialogService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "password-generator",
            Name        = "Password Generator",
            Category    = "Security & Privacy",
            Icon        = "🔒",
            Description = "Generate secure random passwords",
            Factory     = () => new PasswordGeneratorViewModel(ClipboardService),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "ping-tool",
            Name        = "Ping Tool",
            Category    = "Network & Internet",
            Icon        = "🌐",
            Description = "Ping hosts and measure network latency",
            Factory     = () => new PingToolViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key         = "regex-tester",
            Name        = "Regex Tester",
            Category    = "Developer & Productivity",
            Icon        = "💻",
            Description = "Test regular expressions against input text",
            Factory     = () => new RegexTesterViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "text-format-converter",
            Name = "Text Format Converter & Formatter",
            Category = "Developer & Productivity",
            Icon = "🧾",
            Description = "Convert, format, and preview HTML, XML, Markdown, RTF, PDF, DOCX, and JSON.",
            Factory = () => new TextFormatConverterViewModel(
                ClipboardService,
                FileDialogService,
                TextFormatConversionService,
                TextPreviewDocumentBuilder,
                TextPreviewWindowService,
                TextResultExportService,
                UserDialogService),
        });

        // Wire all registered tool keys into the NavigationService.
        ToolRegistry.RegisterAll(NavigationService);
    }
}
