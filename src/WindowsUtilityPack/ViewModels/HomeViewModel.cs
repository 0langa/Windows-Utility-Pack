using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the personalised home dashboard.
/// Exposes favourite tools, recently used tools, category summaries,
/// and the full tool list so the home page is driven dynamically.
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IHomeDashboardService _dashboard;

    private IReadOnlyList<ToolDefinition> _favoriteTools = [];
    private IReadOnlyList<ToolDefinition> _recentTools = [];

    // ── Public collections ────────────────────────────────────────────────────

    /// <summary>User-pinned favourite tools.</summary>
    public IReadOnlyList<ToolDefinition> FavoriteTools
    {
        get => _favoriteTools;
        private set => SetProperty(ref _favoriteTools, value);
    }

    /// <summary>Most recently opened tools (newest first).</summary>
    public IReadOnlyList<ToolDefinition> RecentTools
    {
        get => _recentTools;
        private set => SetProperty(ref _recentTools, value);
    }

    /// <summary>All tools except the "General" category.</summary>
    public IReadOnlyList<ToolDefinition> AllTools { get; }

    /// <summary>Category summaries for the browse-by-category section.</summary>
    public IReadOnlyList<CategoryItem> Categories { get; }

    // ── Visibility helpers ────────────────────────────────────────────────────

    /// <summary>True when the user has at least one favourite.</summary>
    public bool HasFavorites => FavoriteTools.Count > 0;

    /// <summary>True when the recents list is non-empty.</summary>
    public bool HasRecents => RecentTools.Count > 0;

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Navigates to the tool identified by the command parameter key.</summary>
    public RelayCommand NavigateCommand { get; }

    /// <summary>Toggles the favourite state of the tool identified by the command parameter key.</summary>
    public RelayCommand ToggleFavoriteCommand { get; }

    /// <summary>Clears the recently used list.</summary>
    public RelayCommand ClearRecentCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the dashboard ViewModel.
    /// Falls back to static <see cref="App"/> accessors when services are not
    /// injected (DataTemplate instantiation path).
    /// </summary>
    public HomeViewModel(
        INavigationService? navigation = null,
        IHomeDashboardService? dashboard = null)
    {
        _navigation = navigation ?? App.NavigationService;
        _dashboard = dashboard ?? App.HomeDashboardService;

        AllTools = ToolRegistry.GetDisplayTools();
        Categories = ToolRegistry.GetCategories();

        RefreshPersonalisation();

        _dashboard.Changed += OnDashboardChanged;

        NavigateCommand = new RelayCommand(key =>
        {
            var toolKey = key?.ToString() ?? "home";
            _navigation.NavigateTo(toolKey);
        });

        ToggleFavoriteCommand = new RelayCommand(key =>
        {
            if (key is string toolKey)
                _dashboard.ToggleFavorite(toolKey);
        });

        ClearRecentCommand = new RelayCommand(
            _ => _dashboard.ClearRecent(),
            _ => HasRecents);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns whether a tool key is currently favourited (for UI binding).</summary>
    public bool IsFavorite(string toolKey) => _dashboard.IsFavorite(toolKey);

    private void OnDashboardChanged(object? sender, EventArgs e)
        => RefreshPersonalisation();

    private void RefreshPersonalisation()
    {
        FavoriteTools = _dashboard.GetFavoriteTools();
        RecentTools = _dashboard.GetRecentTools();
        OnPropertyChanged(nameof(HasFavorites));
        OnPropertyChanged(nameof(HasRecents));
    }
}
