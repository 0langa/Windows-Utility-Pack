using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the application shell (<c>MainWindow.xaml</c>).
///
/// Responsibilities:
/// <list type="bullet">
///   <item>Exposes <see cref="CurrentView"/> (bound to the content area) — delegates to <see cref="INavigationService"/>.</item>
///   <item>Manages dark/light theme toggling via <see cref="ToggleThemeCommand"/>.</item>
///   <item>Provides <see cref="NavigateCommand"/> for menu/button click handlers.</item>
///   <item>Maintains the <see cref="StatusMessage"/> shown in the status bar.</item>
/// </list>
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private bool _isDarkTheme = true;
    private string _statusMessage = "Ready";

    /// <summary>
    /// Whether the dark theme is currently active.
    /// Setting this property immediately updates <see cref="ThemeToggleIcon"/>.
    /// </summary>
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
    /// Icon shown on the theme toggle button.
    /// ☀ = switch to light mode (currently dark), 🌙 = switch to dark mode (currently light).
    /// </summary>
    public string ThemeToggleIcon => IsDarkTheme ? "☀" : "🌙";

    /// <summary>Text shown in the status bar at the bottom of the window.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// The currently displayed ViewModel.  The ContentControl in MainWindow.xaml is bound
    /// to this property; WPF DataTemplates in App.xaml resolve the appropriate View.
    /// </summary>
    public ViewModelBase? CurrentView => _navigation.CurrentView;

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Toggles between dark and light themes.</summary>
    public RelayCommand ToggleThemeCommand { get; }

    /// <summary>
    /// Navigates to the tool identified by the command parameter string.
    /// Bound to CategoryMenuButton NavigateCommand and home page cards.
    /// </summary>
    public RelayCommand NavigateCommand { get; }

    /// <summary>Navigates unconditionally back to the home screen.</summary>
    public RelayCommand NavigateHomeCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindowViewModel(INavigationService navigation, IThemeService theme)
    {
        _navigation = navigation;
        _theme = theme;

        // Sync the toggle state with whatever theme is already applied.
        _isDarkTheme = theme.CurrentTheme == AppTheme.Dark;

        // When NavigationService reports a navigation, update CurrentView and the status bar.
        _navigation.Navigated += (_, vm) =>
        {
            OnPropertyChanged(nameof(CurrentView));
            StatusMessage = $"Navigated to {vm.GetType().Name.Replace("ViewModel", "")}";
        };

        ToggleThemeCommand  = new RelayCommand(_ => ToggleTheme());
        NavigateCommand     = new RelayCommand(key => _navigation.NavigateTo(key?.ToString() ?? "home"));
        NavigateHomeCommand = new RelayCommand(_ => _navigation.NavigateTo("home"));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _theme.SetTheme(IsDarkTheme ? AppTheme.Dark : AppTheme.Light);
    }
}
