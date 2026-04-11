using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Manages homepage personalization: favorite tools and recently used tools.
/// Backed by <see cref="ISettingsService"/> for persistence across restarts.
/// </summary>
public interface IHomeDashboardService
{
    /// <summary>Maximum number of recent tool entries to retain.</summary>
    int MaxRecentTools { get; }

    /// <summary>Returns the user's favorite tools in pinned order.</summary>
    IReadOnlyList<ToolDefinition> GetFavoriteTools();

    /// <summary>Returns the most recently opened tools (newest first).</summary>
    IReadOnlyList<ToolDefinition> GetRecentTools();

    /// <summary>Returns <see langword="true"/> if the tool with the given key is a favorite.</summary>
    bool IsFavorite(string toolKey);

    /// <summary>Adds or removes a tool from favorites. Returns the new favorite state.</summary>
    bool ToggleFavorite(string toolKey);

    /// <summary>Records a tool launch, pushing it to the top of the recents list.</summary>
    void RecordToolLaunch(string toolKey);

    /// <summary>Clears the entire recent-tools list and persists the change.</summary>
    void ClearRecent();

    /// <summary>Raised after favorites or recents change so the UI can refresh.</summary>
    event EventHandler? Changed;

    // ── Launch count tracking ─────────────────────────────────────────────

    /// <summary>Increments the cumulative launch count for the given tool key.</summary>
    void IncrementLaunchCount(string toolKey);

    /// <summary>Returns the total launch count for a tool key (0 if never launched).</summary>
    int GetLaunchCount(string toolKey);

    /// <summary>Returns all recorded launch counts as a read-only snapshot.</summary>
    IReadOnlyDictionary<string, int> GetAllLaunchCounts();

    // ── Recent search history ─────────────────────────────────────────────

    /// <summary>Returns recent homepage search queries (newest first, capped at 8).</summary>
    IReadOnlyList<string> GetRecentSearches();

    /// <summary>Records a search query. Deduplicates and caps the list at 8 entries.</summary>
    void AddRecentSearch(string query);

    /// <summary>Clears all saved search history.</summary>
    void ClearRecentSearches();
}
