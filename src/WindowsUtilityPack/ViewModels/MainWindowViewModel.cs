using System.Windows;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Views;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the application shell.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private bool _isDarkTheme = true;
    private string _statusMessage = "Ready";

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
                OnPropertyChanged(nameof(ThemeToggleIcon));
        }
    }

    /// <summary>
    /// Segoe MDL2 Assets glyph: sun when dark (switch to light), moon when light (switch to dark).
    /// </summary>
    public string ThemeToggleIcon => IsDarkTheme ? "\uE706" : "\uE793";

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ViewModelBase? CurrentView => _navigation.CurrentView as ViewModelBase;

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand NavigateHomeCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindowViewModel(INavigationService navigation, IThemeService theme)
    {
        _navigation = navigation;
        _theme = theme;

        _isDarkTheme = theme.EffectiveTheme == AppTheme.Dark;

        _navigation.Navigated += (_, vm) =>
        {
            OnPropertyChanged(nameof(CurrentView));
            StatusMessage = $"Navigated to {vm.GetType().Name.Replace("ViewModel", "")}";
        };

        _theme.ThemeChanged += (_, _) =>
        {
            IsDarkTheme = _theme.EffectiveTheme == AppTheme.Dark;
        };

        ToggleThemeCommand  = new RelayCommand(_ => ToggleTheme());
        NavigateCommand     = new RelayCommand(key => _navigation.NavigateTo(key?.ToString() ?? "home"));
        NavigateHomeCommand = new RelayCommand(_ => _navigation.NavigateTo("home"));
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        var newTheme = IsDarkTheme ? AppTheme.Dark : AppTheme.Light;
        _theme.SetTheme(newTheme);

        // Persist immediately so the user doesn't lose their choice on crash.
        var settings = App.SettingsService.Load();
        settings.Theme = newTheme;
        App.SettingsService.Save(settings);
    }

    private static void OpenSettings()
    {
        var window = new SettingsWindow
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
    }
}
