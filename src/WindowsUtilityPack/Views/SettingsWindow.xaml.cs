using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack.Views;

/// <summary>
/// Modal settings window.  Reads settings on open, writes on every change.
/// Kept deliberately simple — no full ViewModel, just INotifyPropertyChanged
/// on the code-behind since this is a standalone dialog.
/// </summary>
public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private AppTheme _selectedTheme;
    private bool _rememberWindowPosition;

    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value) return;
            _selectedTheme = value;
            OnPropertyChanged();
            App.ThemeService.SetTheme(value);
            SaveSettings();
        }
    }

    public bool RememberWindowPosition
    {
        get => _rememberWindowPosition;
        set
        {
            if (_rememberWindowPosition == value) return;
            _rememberWindowPosition = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public SettingsWindow()
    {
        var settings = App.SettingsService.Load();
        _selectedTheme = settings.Theme;
        _rememberWindowPosition = settings.RememberWindowPosition;

        InitializeComponent();
        DataContext = this;
    }

    private void SaveSettings()
    {
        var settings = App.SettingsService.Load();
        settings.Theme = _selectedTheme;
        settings.RememberWindowPosition = _rememberWindowPosition;
        App.SettingsService.Save(settings);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}