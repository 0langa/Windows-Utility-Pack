using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the personalised home dashboard.
/// Drives favourites, recents, category browsing, enhanced search, live system vitals,
/// quick actions, collapsible sections, compact/expanded layout toggle, and usage tracking.
/// </summary>
public class HomeViewModel : ViewModelBase, IDisposable
{
    private static readonly Dictionary<string, string[]> SearchSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["guid"] = ["uuid", "ulid"],
        ["uuid"] = ["guid"],
        ["hash"] = ["checksum", "sha", "md5"],
        ["dns"] = ["domain", "resolver"],
        ["qr"] = ["qrcode", "barcode"],
        ["json"] = ["yaml", "validator", "schema"],
    };

    private readonly INavigationService _navigation;
    private readonly IHomeDashboardService _dashboard;
    private readonly ISettingsService _settings;
    private readonly IClipboardService _clipboard;
    private readonly IUserDialogService? _dialogs;
    private readonly DispatcherTimer _clockTimer;

    private IReadOnlyList<ToolDefinition> _favoriteTools = [];
    private IReadOnlyList<ToolDefinition> _recentTools = [];
    private string _searchQuery = string.Empty;
    private bool _isSearchFocused;
    private IReadOnlyList<ToolDefinition> _searchResults = [];
    private IReadOnlyList<string> _recentSearches = [];
    private IReadOnlyList<ToolDefinition> _recommendedTools = [];
    private CategoryItem? _selectedCategory;
    private IReadOnlyList<ToolDefinition> _selectedCategoryTools = [];
    private IReadOnlyList<HomeCategorySummary> _categorySummaries = [];

    // Section collapse state
    private bool _favoritesExpanded;
    private bool _recentsExpanded;
    private bool _categoryBrowserExpanded;
    private bool _isCompact;

    // Vitals
    private string _cpuDisplay = "—";
    private string _ramDisplay = "—";
    private string _diskDisplay = "—";
    private string _networkDisplay = "—";

    // Greeting / clock
    private string _greetingText = "Hello";
    private string _formattedDate = string.Empty;

    // Quick actions
    private string _clipboardSummary = string.Empty;
    private bool _hasClipboardContent;
    private string _quickPingStatus = "No host detected yet";
    private string? _lastDetectedHost;

    // Usage counts snapshot (refreshed when dashboard changes)
    private IReadOnlyDictionary<string, int> _toolLaunchCounts = new Dictionary<string, int>();

    // ── Public collections ────────────────────────────────────────────────

    public IReadOnlyList<ToolDefinition> FavoriteTools
    {
        get => _favoriteTools;
        private set => SetProperty(ref _favoriteTools, value);
    }

    public IReadOnlyList<ToolDefinition> RecentTools
    {
        get => _recentTools;
        private set => SetProperty(ref _recentTools, value);
    }

    public IReadOnlyList<ToolDefinition> AllTools { get; }
    public IReadOnlyList<CategoryItem> Categories { get; }
    public IReadOnlyList<HomeCategorySummary> CategorySummaries
    {
        get => _categorySummaries;
        private set => SetProperty(ref _categorySummaries, value);
    }

    // ── Search ────────────────────────────────────────────────────────────

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                UpdateSearchResults();
                OnPropertyChanged(nameof(HasSearchQuery));
                OnPropertyChanged(nameof(ShowSearchDropdown));
            }
        }
    }

    /// <summary>Whether the search box is focused (set from code-behind).</summary>
    public bool IsSearchFocused
    {
        get => _isSearchFocused;
        set
        {
            if (SetProperty(ref _isSearchFocused, value))
                OnPropertyChanged(nameof(ShowSearchDropdown));
        }
    }

    public IReadOnlyList<ToolDefinition> SearchResults
    {
        get => _searchResults;
        private set => SetProperty(ref _searchResults, value);
    }

    public IReadOnlyList<string> RecentSearches
    {
        get => _recentSearches;
        private set => SetProperty(ref _recentSearches, value);
    }

    public IReadOnlyList<ToolDefinition> RecommendedTools
    {
        get => _recommendedTools;
        private set => SetProperty(ref _recommendedTools, value);
    }

    public bool HasRecommendations => RecommendedTools.Count > 0;

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(_searchQuery);

    /// <summary>True when the search dropdown popup should be open.</summary>
    public bool ShowSearchDropdown =>
        (HasSearchQuery && SearchResults.Count > 0) ||
        (!HasSearchQuery && IsSearchFocused && RecentSearches.Count > 0);

    // ── Category selection ────────────────────────────────────────────────

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

    public IReadOnlyList<ToolDefinition> SelectedCategoryTools
    {
        get => _selectedCategoryTools;
        private set => SetProperty(ref _selectedCategoryTools, value);
    }

    public bool HasSelectedCategory => SelectedCategory is not null;

    // ── Visibility helpers ────────────────────────────────────────────────

    public bool HasFavorites => FavoriteTools.Count > 0;
    public bool HasRecents => RecentTools.Count > 0;

    // ── Section collapse state ────────────────────────────────────────────

    public bool FavoritesExpanded
    {
        get => _favoritesExpanded;
        set
        {
            if (SetProperty(ref _favoritesExpanded, value))
                SaveViewPrefs();
        }
    }

    public bool RecentsExpanded
    {
        get => _recentsExpanded;
        set
        {
            if (SetProperty(ref _recentsExpanded, value))
                SaveViewPrefs();
        }
    }

    public bool CategoryBrowserExpanded
    {
        get => _categoryBrowserExpanded;
        set
        {
            if (SetProperty(ref _categoryBrowserExpanded, value))
                SaveViewPrefs();
        }
    }

    public bool IsCompact
    {
        get => _isCompact;
        set
        {
            if (SetProperty(ref _isCompact, value))
                SaveViewPrefs();
        }
    }

    // ── Vitals ────────────────────────────────────────────────────────────

    public string CpuDisplay
    {
        get => _cpuDisplay;
        private set => SetProperty(ref _cpuDisplay, value);
    }

    public string RamDisplay
    {
        get => _ramDisplay;
        private set => SetProperty(ref _ramDisplay, value);
    }

    public string DiskDisplay
    {
        get => _diskDisplay;
        private set => SetProperty(ref _diskDisplay, value);
    }

    public string NetworkDisplay
    {
        get => _networkDisplay;
        private set => SetProperty(ref _networkDisplay, value);
    }

    // ── Greeting / clock ─────────────────────────────────────────────────

    public string GreetingText
    {
        get => _greetingText;
        private set => SetProperty(ref _greetingText, value);
    }

    public string FormattedDate
    {
        get => _formattedDate;
        private set => SetProperty(ref _formattedDate, value);
    }

    // ── Quick actions ─────────────────────────────────────────────────────

    public string ClipboardSummary
    {
        get => _clipboardSummary;
        private set => SetProperty(ref _clipboardSummary, value);
    }

    public bool HasClipboardContent
    {
        get => _hasClipboardContent;
        private set => SetProperty(ref _hasClipboardContent, value);
    }

    public string QuickPingStatus
    {
        get => _quickPingStatus;
        private set => SetProperty(ref _quickPingStatus, value);
    }

    // ── Usage counts (for usage-frequency dots on tool cards) ─────────────

    public IReadOnlyDictionary<string, int> ToolLaunchCounts
    {
        get => _toolLaunchCounts;
        private set => SetProperty(ref _toolLaunchCounts, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────

    public RelayCommand NavigateCommand { get; }
    public RelayCommand ToggleFavoriteCommand { get; }
    public RelayCommand ClearRecentCommand { get; }
    public RelayCommand SelectCategoryCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand ExecuteRecentSearchCommand { get; }
    public RelayCommand ClearRecentSearchesCommand { get; }
    public RelayCommand CopyToolNameCommand { get; }
    public RelayCommand ToggleFavoritesExpandedCommand { get; }
    public RelayCommand ToggleRecentsExpandedCommand { get; }
    public RelayCommand ToggleCategoryBrowserExpandedCommand { get; }
    public RelayCommand ToggleCompactModeCommand { get; }
    public RelayCommand QuickGeneratePasswordCommand { get; }
    public RelayCommand QuickGenerateUuidCommand { get; }
    public RelayCommand InspectClipboardCommand { get; }
    public AsyncRelayCommand QuickPingHostCommand { get; }
    public RelayCommand ViewDescriptionCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────

    public HomeViewModel(
        INavigationService navigation,
        IHomeDashboardService dashboard,
        ISettingsService settings,
        IClipboardService clipboard,
        IUserDialogService? dialogs = null)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialogs = dialogs;

        AllTools = ToolRegistry.GetDisplayTools();
        Categories = ToolRegistry.GetCategories();

        if (Categories.Count > 0)
            SelectedCategory = Categories[0];

        // Load persisted view preferences.
        var appSettings = _settings.Load();
        _favoritesExpanded = appSettings.FavoritesExpanded;
        _recentsExpanded = appSettings.RecentsExpanded;
        _categoryBrowserExpanded = appSettings.CategoryBrowserExpanded;
        _isCompact = appSettings.HomeViewIsCompact;

        RefreshPersonalisation();
        RefreshCategorySummaries();

        _dashboard.Changed += OnDashboardChanged;

        // Subscribe to vitals service if available.
        if (App.VitalsService is { } vitals)
        {
            vitals.Updated += OnVitalsUpdated;
            OnVitalsUpdated(vitals, EventArgs.Empty);
        }

        // Clock and greeting.
        UpdateGreeting();
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _clockTimer.Tick += (_, _) => UpdateGreeting();
        _clockTimer.Start();

        // ── Commands ──────────────────────────────────────────────────────

        NavigateCommand = new RelayCommand(key =>
        {
            var toolKey = key?.ToString() ?? "home";
            // Record search query before clearing it.
            if (HasSearchQuery)
            {
                _dashboard.AddRecentSearch(_searchQuery.Trim());
                SearchQuery = string.Empty;
            }
            _navigation.NavigateTo(toolKey);
        });

        ToggleFavoriteCommand = new RelayCommand(key =>
        {
            if (key is string toolKey) _dashboard.ToggleFavorite(toolKey);
        });

        ClearRecentCommand = new RelayCommand(
            _ => _dashboard.ClearRecent(),
            _ => HasRecents);

        SelectCategoryCommand = new RelayCommand(param =>
        {
            if (param is CategoryItem category)
                SelectedCategory = SelectedCategory == category ? null : category;
        });

        ClearSearchCommand = new RelayCommand(_ => SearchQuery = string.Empty);

        ExecuteRecentSearchCommand = new RelayCommand(param =>
        {
            if (param is string query) SearchQuery = query;
        });

        ClearRecentSearchesCommand = new RelayCommand(_ =>
        {
            _dashboard.ClearRecentSearches();
            RecentSearches = [];
        });

        CopyToolNameCommand = new RelayCommand(key =>
        {
            if (key is string toolKey)
            {
                var tool = ToolRegistry.GetByKey(toolKey);
                if (tool is not null) _clipboard.SetText(tool.Name);
            }
        });

        ToggleFavoritesExpandedCommand = new RelayCommand(_ => FavoritesExpanded = !FavoritesExpanded);
        ToggleRecentsExpandedCommand = new RelayCommand(_ => RecentsExpanded = !RecentsExpanded);
        ToggleCategoryBrowserExpandedCommand = new RelayCommand(_ => CategoryBrowserExpanded = !CategoryBrowserExpanded);
        ToggleCompactModeCommand = new RelayCommand(_ => IsCompact = !IsCompact);

        QuickGeneratePasswordCommand = new RelayCommand(_ =>
        {
            var pw = GeneratePassword(20);
            _clipboard.SetText(pw);
        });

        QuickGenerateUuidCommand = new RelayCommand(_ =>
        {
            _clipboard.SetText(Guid.NewGuid().ToString("D").ToUpperInvariant());
        });

        InspectClipboardCommand = new RelayCommand(_ => RefreshClipboardSummary());

        QuickPingHostCommand = new AsyncRelayCommand(_ => QuickPingHostAsync());

        ViewDescriptionCommand = new RelayCommand(key =>
        {
            if (key is not string toolKey) return;
            var tool = ToolRegistry.GetByKey(toolKey);
            if (tool is null) return;

            if (_dialogs is not null)
            {
                _dialogs.ShowInfo(tool.Name, tool.Description);
            }
            else
            {
                _clipboard.SetText($"{tool.Name}: {tool.Description}");
            }
        });

        RefreshClipboardSummary();
    }

    // ── Public helpers ────────────────────────────────────────────────────

    public bool IsFavorite(string toolKey) => _dashboard.IsFavorite(toolKey);

    // ── Private helpers ───────────────────────────────────────────────────

    private void OnDashboardChanged(object? sender, EventArgs e)
    {
        RefreshPersonalisation();
        RecentSearches = _dashboard.GetRecentSearches();
        RefreshCategorySummaries();
        RefreshRecommendations();
    }

    private void RefreshPersonalisation()
    {
        FavoriteTools = _dashboard.GetFavoriteTools();
        RecentTools = _dashboard.GetRecentTools();
        ToolLaunchCounts = _dashboard.GetAllLaunchCounts();
        OnPropertyChanged(nameof(HasFavorites));
        OnPropertyChanged(nameof(HasRecents));
        RefreshRecommendations();
        OnPropertyChanged(nameof(HasRecommendations));
    }

    private void RefreshCategorySummaries()
    {
        var summaries = new List<HomeCategorySummary>(Categories.Count);
        foreach (var category in Categories)
        {
            var orderedTools = category.Tools
                .OrderByDescending(t => ToolLaunchCounts.TryGetValue(t.Key, out var count) ? count : 0)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var topTool = orderedTools.FirstOrDefault();
            var topToolCount = topTool is not null && ToolLaunchCounts.TryGetValue(topTool.Key, out var c) ? c : 0;
            var mostUsedLabel = topToolCount > 0 && topTool is not null
                ? topTool.Name
                : "No usage data yet";

            summaries.Add(new HomeCategorySummary
            {
                Category = category,
                ToolCount = category.Tools.Count,
                MostUsedToolName = mostUsedLabel,
                TopToolNames = orderedTools.Take(3).Select(t => t.Name).ToList(),
            });
        }

        CategorySummaries = summaries;
    }

    private void RefreshRecommendations()
    {
        var favoriteKeys = FavoriteTools.Select(tool => tool.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recentKeys = RecentTools.Select(tool => tool.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var recommended = AllTools
            .Where(tool =>
                !tool.Key.Equals("home", StringComparison.OrdinalIgnoreCase)
                && !favoriteKeys.Contains(tool.Key)
                && !recentKeys.Contains(tool.Key))
            .OrderByDescending(tool => ToolLaunchCounts.TryGetValue(tool.Key, out var count) ? count : 0)
            .ThenBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        RecommendedTools = recommended;
    }

    private void UpdateSearchResults()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            SearchResults = [];
            RecentSearches = _dashboard.GetRecentSearches();
            OnPropertyChanged(nameof(ShowSearchDropdown));
            return;
        }

        var query = _searchQuery.Trim();
        var results = new List<ToolDefinition>();

        foreach (var tool in AllTools)
        {
            var corpus = string.Join(' ', new[]
            {
                tool.Name,
                tool.Description,
                tool.Category,
                tool.Key.Replace('-', ' '),
                string.Join(' ', tool.Keywords),
            });

            var directMatch = corpus.Contains(query, StringComparison.OrdinalIgnoreCase);
            var synonymMatch = SearchSynonyms.TryGetValue(query, out var synonyms)
                && synonyms.Any(s => corpus.Contains(s, StringComparison.OrdinalIgnoreCase));

            if (directMatch || synonymMatch)
            {
                results.Add(tool);
            }
        }

        SearchResults = results;
        OnPropertyChanged(nameof(ShowSearchDropdown));
    }

    private void OnVitalsUpdated(object? sender, EventArgs e)
    {
        if (App.VitalsService is not { } v) return;

        CpuDisplay = v.CpuPercent >= 0 ? $"{v.CpuPercent:F0}%" : "—";

        if (v.RamFreeGb >= 0 && v.TotalRamGb > 0)
            RamDisplay = $"{v.RamFreeGb:F1}/{v.TotalRamGb:F0} GB free";
        else
            RamDisplay = "—";

        DiskDisplay = v.DiskFreeGb >= 0 ? $"{v.DiskFreeGb:F1} GB free" : "—";

        if (v.NetworkReceiveKbps >= 0)
        {
            var down = FormatNetworkSpeed(v.NetworkReceiveKbps);
            var up = FormatNetworkSpeed(v.NetworkSendKbps);
            NetworkDisplay = $"↓{down} ↑{up}";
        }
        else
        {
            NetworkDisplay = "—";
        }
    }

    private static string FormatNetworkSpeed(double kbps) => kbps switch
    {
        >= 1024 * 1024 => $"{kbps / (1024 * 1024):F1} GB/s",
        >= 1024        => $"{kbps / 1024:F1} MB/s",
        _              => $"{kbps:F0} KB/s",
    };

    private void UpdateGreeting()
    {
        var now = DateTime.Now;
        GreetingText = now.Hour switch
        {
            >= 5 and < 12  => "Good morning",
            >= 12 and < 18 => "Good afternoon",
            _              => "Good evening",
        };
        FormattedDate = now.ToString("dddd, MMMM d");
    }

    private void RefreshClipboardSummary()
    {
        if (!_clipboard.TryGetText(out var text) || string.IsNullOrEmpty(text))
        {
            ClipboardSummary = "Clipboard is empty";
            HasClipboardContent = false;
            _lastDetectedHost = null;
            return;
        }

        HasClipboardContent = true;
        var trimmed = text.Trim();
        var charCount = text.Length;
        var type = DetectContentType(trimmed);
        ClipboardSummary = $"{charCount:N0} chars · {type}";

        _lastDetectedHost = TryExtractHost(trimmed);
        if (_lastDetectedHost is not null)
            QuickPingStatus = $"Ready: {_lastDetectedHost}";
    }

    private static string DetectContentType(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "empty";
        if (Uri.TryCreate(text, UriKind.Absolute, out _)) return "URL";
        if (Guid.TryParse(text, out _)) return "UUID";
        if (IsHex(text) && text.Length is 32 or 40 or 64) return "hash";
        if (IsBase64(text)) return "Base64";
        if (text.Contains('\n')) return "multiline text";
        return "text";
    }

    private static bool IsHex(string s)
        => s.Length > 0 && s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));

    private static readonly Regex Base64Pattern = new(@"^[A-Za-z0-9+/]+=*$", RegexOptions.Compiled);
    private static bool IsBase64(string s)
        => s.Length >= 4 && s.Length % 4 == 0 && Base64Pattern.IsMatch(s);

    private static string GeneratePassword(int length)
    {
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string symbols = "!@#$%^&*()-_=+[]{}";
        var charset = upper + lower + digits + symbols;
        var charsetBytes = charset.ToCharArray();

        var result = new char[length];
        // Guarantee at least one of each required category.
        result[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        result[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        result[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        result[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

        for (int i = 4; i < length; i++)
            result[i] = charsetBytes[RandomNumberGenerator.GetInt32(charsetBytes.Length)];

        // Shuffle the array.
        for (int i = length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return new string(result);
    }

    private void SaveViewPrefs()
    {
        try
        {
            var appSettings = _settings.Load();
            appSettings.HomeViewIsCompact = _isCompact;
            appSettings.FavoritesExpanded = _favoritesExpanded;
            appSettings.RecentsExpanded = _recentsExpanded;
            appSettings.CategoryBrowserExpanded = _categoryBrowserExpanded;
            _settings.Save(appSettings);
        }
        catch { }
    }

    private async Task QuickPingHostAsync()
    {
        RefreshClipboardSummary();

        var host = _lastDetectedHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            QuickPingStatus = "No URL/host detected in clipboard";
            return;
        }

        QuickPingStatus = $"Pinging {host}...";
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 3000).ConfigureAwait(true);
            if (reply.Status == IPStatus.Success)
            {
                QuickPingStatus = $"{host}: {reply.RoundtripTime} ms";
            }
            else
            {
                QuickPingStatus = $"{host}: {reply.Status}";
            }
        }
        catch (Exception ex)
        {
            QuickPingStatus = $"{host}: {ex.Message}";
        }
    }

    private static string? TryExtractHost(string text)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute))
            return absolute.Host;

        if (Uri.TryCreate($"https://{text}", UriKind.Absolute, out var normalized)
            && !string.IsNullOrWhiteSpace(normalized.Host))
        {
            return normalized.Host;
        }

        return null;
    }

    public void Dispose()
    {
        _dashboard.Changed -= OnDashboardChanged;
        if (App.VitalsService is { } vitals)
            vitals.Updated -= OnVitalsUpdated;

        _clockTimer.Stop();
    }
}
