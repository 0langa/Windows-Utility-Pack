using System.Windows;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack;

/// <summary>
/// Application entry point and DI/service bootstrapping.
/// </summary>
public partial class App : Application
{
    public static IThemeService ThemeService { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService = new ThemeService();
    }
}
