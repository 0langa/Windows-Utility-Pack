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
}
