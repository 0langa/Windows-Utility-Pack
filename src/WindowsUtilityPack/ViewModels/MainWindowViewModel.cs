using System.Collections.ObjectModel;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Controls;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;

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
///   <item>Exposes <see cref="Categories"/> derived from <see cref="ToolRegistry"/> to drive the nav bar.</item>
/// </list>
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private bool _isDarkTheme = true;
    private string _statusMessage = "Ready";

    // Per-category icons for the navigation bar.
    // Defined here so there is exactly one place to update them rather than
    // duplicating the same mapping across multiple XAML files.
    private static readonly IReadOnlyDictionary<string, string> _categoryIcons =
        new Dictionary<string, string>
        {
            ["System Utilities"]         = "🖥",
            ["File & Data Tools"]        = "📁",
            ["Security & Privacy"]       = "🔒",
            ["Network & Internet"]       = "🌐",
            ["Developer & Productivity"] = "💻",
        };

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

    /// <summary>
    /// Navigation categories derived from <see cref="ToolRegistry"/> metadata.
    /// Drives the shell's category navigation bar via data binding instead of hard-coded XAML.
    /// </summary>
    public IReadOnlyList<CategoryNavItem> Categories { get; }

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

        // Build navigation categories from ToolRegistry metadata.
        // Excludes "home" (a special shell destination, not a navigable category).
        // GroupBy preserves registration order so categories appear in the same
        // sequence as they were registered in App.xaml.cs.
        Categories = ToolRegistry.All
            .Where(t => t.Key != "home")
            .GroupBy(t => t.Category)
            .Select(g => new CategoryNavItem
            {
                Label    = g.Key,
                Icon     = _categoryIcons.TryGetValue(g.Key, out var icon) ? icon : g.First().Icon,
                MenuItems = new ObservableCollection<MenuEntry>(
                    g.Select(t => new MenuEntry { Label = t.Name, ToolKey = t.Key })),
            })
            .ToList()
            .AsReadOnly();

        // When NavigationService reports a navigation, update CurrentView and the status bar.
        // Use ToolDefinition.Name for user-visible text rather than deriving it from the
        // runtime type name, so the label is intentional and decoupled from class naming.
        _navigation.Navigated += (_, vm) =>
        {
            OnPropertyChanged(nameof(CurrentView));
            var toolName = ToolRegistry.All
                .FirstOrDefault(t => t.Key == _navigation.CurrentKey)
                ?.Name ?? vm.GetType().Name.Replace("ViewModel", "");
            StatusMessage = $"Navigated to {toolName}";
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
