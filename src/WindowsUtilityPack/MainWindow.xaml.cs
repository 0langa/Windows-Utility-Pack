    using System.Windows;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack;

/// <summary>
/// Code-behind for the application shell window.
///
/// The window is deliberately thin — all business logic lives in
/// <see cref="MainWindowViewModel"/>.  This file only handles:
/// <list type="bullet">
///   <item>Constructing the ViewModel with the required services.</item>
///   <item>Restoring saved window geometry from settings.</item>
///   <item>Persisting window state (size, position, theme) when the window closes.</item>
/// </list>
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainWindowViewModel(App.NavigationService, App.ThemeService);
        DataContext = vm;

        var settings = App.SettingsService.Load();
        if (settings.RememberWindowPosition)
        {
            if (!double.IsNaN(settings.WindowLeft)) Left   = settings.WindowLeft;
            if (!double.IsNaN(settings.WindowTop))  Top    = settings.WindowTop;
            Width  = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        App.NavigationService.NavigateTo("home");
    }

    private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = App.SettingsService.Load();
        settings.Theme = App.ThemeService.CurrentTheme;

        if (settings.RememberWindowPosition)
        {
            settings.WindowLeft   = Left;
            settings.WindowTop    = Top;
            settings.WindowWidth  = Width;
            settings.WindowHeight = Height;
        }

        App.SettingsService.Save(settings);
    }
}
