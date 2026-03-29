using System.Windows;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.Tools.SystemUtilities.DiskInfo;
using WindowsUtilityPack.Tools.FileDataTools.BulkFileRenamer;
using WindowsUtilityPack.Tools.SecurityPrivacy.PasswordGenerator;
using WindowsUtilityPack.Tools.NetworkInternet.PingTool;
using WindowsUtilityPack.Tools.DeveloperProductivity.RegexTester;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack;

public partial class App : Application
{
    public static IThemeService ThemeService { get; private set; } = null!;
    public static INavigationService NavigationService { get; private set; } = null!;
    public static ISettingsService SettingsService { get; private set; } = null!;
    public static ILoggingService LoggingService { get; private set; } = null!;
    public static INotificationService NotificationService { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LoggingService = new LoggingService();
        SettingsService = new SettingsService();
        NavigationService = new NavigationService();
        ThemeService = new ThemeService();
        NotificationService = new NotificationService();

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

    private static void RegisterTools()
    {
        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "home",
            Name = "Home",
            Category = "General",
            Icon = "🏠",
            Description = "Application dashboard",
            Factory = () => new HomeViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "disk-info",
            Name = "Disk Info Viewer",
            Category = "System Utilities",
            Icon = "💾",
            Description = "View drive information and disk usage",
            Factory = () => new DiskInfoViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "bulk-renamer",
            Name = "Bulk File Renamer",
            Category = "File & Data Tools",
            Icon = "📁",
            Description = "Rename multiple files with prefix/suffix/find-replace",
            Factory = () => new BulkFileRenamerViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "password-generator",
            Name = "Password Generator",
            Category = "Security & Privacy",
            Icon = "🔒",
            Description = "Generate secure random passwords",
            Factory = () => new PasswordGeneratorViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "ping-tool",
            Name = "Ping Tool",
            Category = "Network & Internet",
            Icon = "🌐",
            Description = "Ping hosts and measure network latency",
            Factory = () => new PingToolViewModel(),
        });

        ToolRegistry.Register(new Models.ToolDefinition
        {
            Key = "regex-tester",
            Name = "Regex Tester",
            Category = "Developer & Productivity",
            Icon = "💻",
            Description = "Test regular expressions against input text",
            Factory = () => new RegexTesterViewModel(),
        });

        ToolRegistry.RegisterAll(NavigationService);
    }
}
