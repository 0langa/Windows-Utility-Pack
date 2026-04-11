using WindowsUtilityPack.Models;
using WindowsUtilityPack.Tools;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Default implementation of <see cref="IHomeDashboardService"/>.
/// Persists favorite and recent tool keys via <see cref="ISettingsService"/>
/// and resolves them to <see cref="ToolDefinition"/> through <see cref="ToolRegistry"/>.
/// </summary>
public sealed class HomeDashboardService : IHomeDashboardService
{
    private readonly ISettingsService _settings;
    private readonly List<string> _favoriteKeys;
    private readonly List<string> _recentKeys;

    /// <inheritdoc />
    public int MaxRecentTools => 10;

    /// <inheritdoc />
    public event EventHandler? Changed;

    public HomeDashboardService(ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        var appSettings = settings.Load();
        _favoriteKeys = new List<string>(appSettings.FavoriteToolKeys ?? []);
        _recentKeys = new List<string>(appSettings.RecentToolKeys ?? []);

        // Prune keys that no longer exist in the registry.
        _favoriteKeys.RemoveAll(k => ToolRegistry.GetByKey(k) is null);
        _recentKeys.RemoveAll(k => ToolRegistry.GetByKey(k) is null);
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolDefinition> GetFavoriteTools()
        => ResolveKeys(_favoriteKeys);

    /// <inheritdoc />
    public IReadOnlyList<ToolDefinition> GetRecentTools()
        => ResolveKeys(_recentKeys);

    /// <inheritdoc />
    public bool IsFavorite(string toolKey)
        => _favoriteKeys.Contains(toolKey, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool ToggleFavorite(string toolKey)
    {
        ArgumentNullException.ThrowIfNull(toolKey);

        if (ToolRegistry.GetByKey(toolKey) is null)
            return false;

        var index = _favoriteKeys.FindIndex(k => k.Equals(toolKey, StringComparison.OrdinalIgnoreCase));
        bool isFavoriteNow;

        if (index >= 0)
        {
            _favoriteKeys.RemoveAt(index);
            isFavoriteNow = false;
        }
        else
        {
            _favoriteKeys.Add(toolKey);
            isFavoriteNow = true;
        }

        Persist();
        Changed?.Invoke(this, EventArgs.Empty);
        return isFavoriteNow;
    }

    /// <inheritdoc />
    public void RecordToolLaunch(string toolKey)
    {
        ArgumentNullException.ThrowIfNull(toolKey);

        // Ignore "home" and unknown tools.
        if (toolKey.Equals("home", StringComparison.OrdinalIgnoreCase))
            return;
        if (ToolRegistry.GetByKey(toolKey) is null)
            return;

        // Move to front (newest first), deduplicating.
        _recentKeys.RemoveAll(k => k.Equals(toolKey, StringComparison.OrdinalIgnoreCase));
        _recentKeys.Insert(0, toolKey);

        // Trim to cap.
        while (_recentKeys.Count > MaxRecentTools)
            _recentKeys.RemoveAt(_recentKeys.Count - 1);

        Persist();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void ClearRecent()
    {
        _recentKeys.Clear();
        Persist();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Persist()
    {
        var appSettings = _settings.Load();
        appSettings.FavoriteToolKeys = new List<string>(_favoriteKeys);
        appSettings.RecentToolKeys = new List<string>(_recentKeys);
        _settings.Save(appSettings);
    }

    private static List<ToolDefinition> ResolveKeys(List<string> keys)
    {
        var result = new List<ToolDefinition>(keys.Count);
        foreach (var key in keys)
        {
            var tool = ToolRegistry.GetByKey(key);
            if (tool is not null)
                result.Add(tool);
        }
        return result;
    }
}
