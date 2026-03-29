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

        // Construct the ViewModel and inject the shared service instances.
        var vm = new MainWindowViewModel(App.NavigationService, App.ThemeService);
        DataContext = vm;

        // Apply saved window position and size; ignore NaN values (first run).
        var settings = App.SettingsService.Load();
        if (!double.IsNaN(settings.WindowLeft)) Left   = settings.WindowLeft;
        if (!double.IsNaN(settings.WindowTop))  Top    = settings.WindowTop;
        Width  = settings.WindowWidth;
        Height = settings.WindowHeight;

        // Navigate to the home screen as the initial content.
        App.NavigationService.NavigateTo("home");
    }

    /// <summary>
    /// Persists the current window state and theme preference before the window closes.
    /// Called by the <c>Closing</c> event wired in <c>MainWindow.xaml</c>.
    /// </summary>
    private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = App.SettingsService.Load();
        settings.Theme        = App.ThemeService.CurrentTheme;
        settings.WindowLeft   = Left;
        settings.WindowTop    = Top;
        settings.WindowWidth  = Width;
        settings.WindowHeight = Height;
        App.SettingsService.Save(settings);
    }
}
