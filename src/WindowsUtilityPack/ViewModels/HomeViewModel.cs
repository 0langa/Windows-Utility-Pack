using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the personalised home dashboard.
/// Exposes favourite tools, recently used tools, category summaries,
/// tool search, category selection, and the full tool list so the
/// home page is driven dynamically from a single source of truth.
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IHomeDashboardService _dashboard;

    private IReadOnlyList<ToolDefinition> _favoriteTools = [];
    private IReadOnlyList<ToolDefinition> _recentTools = [];
    private string _searchQuery = string.Empty;
    private IReadOnlyList<ToolDefinition> _searchResults = [];
    private CategoryItem? _selectedCategory;
    private IReadOnlyList<ToolDefinition> _selectedCategoryTools = [];

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

    // ── Search ────────────────────────────────────────────────────────────────

    /// <summary>Current search query text. Updates results on every change.</summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                UpdateSearchResults();
                OnPropertyChanged(nameof(HasSearchQuery));
                OnPropertyChanged(nameof(ShowSearchResults));
            }
        }
    }

    /// <summary>Tools matching the current search query.</summary>
    public IReadOnlyList<ToolDefinition> SearchResults
    {
        get => _searchResults;
        private set => SetProperty(ref _searchResults, value);
    }

    /// <summary>True when the search box contains text.</summary>
    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(_searchQuery);

    /// <summary>True when search results should be displayed.</summary>
    public bool ShowSearchResults => HasSearchQuery && SearchResults.Count > 0;

    // ── Category selection ────────────────────────────────────────────────────

    /// <summary>Currently selected category tab (null = none selected).</summary>
    public CategoryItem? SelectedCategory
    {
        get => _selectedCategory;
        private set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                SelectedCategoryTools = value?.Tools ?? [];
                OnPropertyChanged(nameof(HasSelectedCategory));
            }
        }
    }

    /// <summary>Tools belonging to the currently selected category.</summary>
    public IReadOnlyList<ToolDefinition> SelectedCategoryTools
    {
        get => _selectedCategoryTools;
        private set => SetProperty(ref _selectedCategoryTools, value);
    }

    /// <summary>True when a category tab is selected.</summary>
    public bool HasSelectedCategory => SelectedCategory is not null;

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

    /// <summary>Selects a category tab by <see cref="CategoryItem"/> parameter. Toggles off if already selected.</summary>
    public RelayCommand SelectCategoryCommand { get; }

    /// <summary>Clears the search query.</summary>
    public RelayCommand ClearSearchCommand { get; }

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

        // Auto-select first category so the panel is never empty on load.
        if (Categories.Count > 0)
            SelectedCategory = Categories[0];

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

        SelectCategoryCommand = new RelayCommand(param =>
        {
            if (param is CategoryItem category)
            {
                // Toggle: clicking the already-selected tab deselects it.
                SelectedCategory = SelectedCategory == category ? null : category;
            }
        });

        ClearSearchCommand = new RelayCommand(_ => SearchQuery = string.Empty);
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

    private void UpdateSearchResults()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            SearchResults = [];
            OnPropertyChanged(nameof(ShowSearchResults));
            return;
        }

        var query = _searchQuery.Trim();
        var results = new List<ToolDefinition>();

        foreach (var tool in AllTools)
        {
            if (tool.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || tool.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || tool.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(tool);
            }
        }

        SearchResults = results;
        OnPropertyChanged(nameof(ShowSearchResults));
    }
}
