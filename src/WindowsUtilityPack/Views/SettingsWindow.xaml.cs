using System.Windows;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Views;

/// <summary>
/// Modal settings window.  Reads settings on open, writes on every change.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsWindowViewModel(App.SettingsService, App.ThemeService);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
