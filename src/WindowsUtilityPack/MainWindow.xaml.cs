using System.Windows;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainWindowViewModel(App.NavigationService, App.ThemeService);
        DataContext = vm;

        // Apply saved window position/size
        var settings = App.SettingsService.Load();
        if (!double.IsNaN(settings.WindowLeft)) Left = settings.WindowLeft;
        if (!double.IsNaN(settings.WindowTop)) Top = settings.WindowTop;
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        // Navigate to home on startup
        App.NavigationService.NavigateTo("home");
    }

    private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = App.SettingsService.Load();
        settings.Theme = App.ThemeService.CurrentTheme;
        settings.WindowLeft = Left;
        settings.WindowTop = Top;
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
        App.SettingsService.Save(settings);
    }
}
