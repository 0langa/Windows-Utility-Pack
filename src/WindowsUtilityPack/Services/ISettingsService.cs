using WindowsUtilityPack.Services.Downloader;

namespace WindowsUtilityPack.Services;

/// <summary>Persisted user preferences loaded and saved as JSON.</summary>
public class AppSettings
{
    /// <summary>Whether custom shell hotkeys are enabled.</summary>
    public bool HotkeysEnabled { get; set; } = true;

    /// <summary>Persisted shell hotkey bindings.</summary>
    public List<HotkeyBindingSetting> HotkeyBindings { get; set; } = [];

    /// <summary>Last used colour theme (default: Dark).</summary>
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>Tool key to navigate to on startup (default: "home").</summary>
    public string StartupPage { get; set; } = "home";

    /// <summary>Whether the app saves/restores window position and size.</summary>
    public bool RememberWindowPosition { get; set; } = true;

    /// <summary>Saved window left position, or <see cref="double.NaN"/> if unset.</summary>
    public double WindowLeft { get; set; } = double.NaN;

    /// <summary>Saved window top position, or <see cref="double.NaN"/> if unset.</summary>
    public double WindowTop { get; set; } = double.NaN;

    /// <summary>Saved window width (default: 1100).</summary>
    public double WindowWidth { get; set; } = 1100;

    /// <summary>Saved window height (default: 700).</summary>
    public double WindowHeight { get; set; } = 700;

    /// <summary>Most recently generated QR URLs (newest first).</summary>
    public List<string> QrCodeRecentUrls { get; set; } = [];

    /// <summary>Last directory used by the QR code exporter.</summary>
    public string QrCodeLastExportDirectory { get; set; } = string.Empty;

    /// <summary>Whether QR exports append a timestamp to suggested filenames.</summary>
    public bool QrCodeIncludeTimestampInFileName { get; set; } = true;

    /// <summary>Persisted settings for the Downloader module.</summary>
    public DownloaderSettings DownloaderSettings { get; set; } = new();

    /// <summary>Tool keys the user has pinned as favorites (order preserved).</summary>
    public List<string> FavoriteToolKeys { get; set; } = [];

    /// <summary>
    /// Most recently opened tool keys (newest first).
    /// Capped at <see cref="IHomeDashboardService.MaxRecentTools"/> entries.
    /// </summary>
    public List<string> RecentToolKeys { get; set; } = [];

    // ── Homepage view preferences ─────────────────────────────────────────

    /// <summary>Whether the All Tools grid uses compact row layout (true) or rich card grid (false).</summary>
    public bool HomeViewIsCompact { get; set; } = false;

    /// <summary>Whether the Favourites section is expanded on the homepage.</summary>
    public bool FavoritesExpanded { get; set; } = true;

    /// <summary>Whether the Recently Used section is expanded on the homepage.</summary>
    public bool RecentsExpanded { get; set; } = true;

    /// <summary>Whether the Browse by Category section is expanded on the homepage.</summary>
    public bool CategoryBrowserExpanded { get; set; } = true;

    /// <summary>Last N search queries for the homepage search dropdown (newest first).</summary>
    public List<string> HomeRecentSearches { get; set; } = [];

    /// <summary>
    /// Cumulative launch count per tool key.
    /// Used for the usage-frequency indicator on tool cards.
    /// </summary>
    public Dictionary<string, int> ToolLaunchCounts { get; set; } = [];
}

/// <summary>
/// Persisted shell hotkey binding setting.
/// </summary>
public class HotkeyBindingSetting
{
    public string Action { get; set; } = string.Empty;

    public string Gesture { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Contract for loading and persisting application settings.
/// Settings are stored as JSON in <c>%LOCALAPPDATA%\WindowsUtilityPack\settings.json</c>.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads settings from disk.  Returns defaults silently if the file is absent or corrupt.
    /// </summary>
    AppSettings Load();

    /// <summary>
    /// Persists the supplied settings to disk.
    /// Failures (e.g. permissions) are swallowed silently to prevent startup crashes.
    /// </summary>
    void Save(AppSettings settings);
}
