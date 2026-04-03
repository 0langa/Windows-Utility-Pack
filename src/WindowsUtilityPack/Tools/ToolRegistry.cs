using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack.Tools;

/// <summary>
/// Central registry of all available tools in the application.
///
/// How it works:
/// <list type="number">
///   <item><c>App.xaml.cs</c> calls <see cref="Register"/> once per tool during startup.</item>
///   <item><see cref="RegisterAll"/> is then called to push every tool key + factory into
///         the <see cref="INavigationService"/>, enabling navigation by key string.</item>
///   <item>The UI (home page cards, category menu entries) is driven dynamically from
///         <see cref="All"/> and <see cref="GetCategories"/> instead of being hard-coded.</item>
/// </list>
/// </summary>
public static class ToolRegistry
{
    private static readonly List<ToolDefinition> _tools = [];

    // Category display order and MDL2 icon mappings.
    // Categories appear in the order they are first encountered during registration.
    private static readonly Dictionary<string, string> _categoryIcons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>All registered tool definitions, in registration order.</summary>
    public static IReadOnlyList<ToolDefinition> All => _tools;

    /// <summary>
    /// Adds a tool to the registry.
    /// Called once per tool in <c>App.xaml.cs RegisterTools()</c>.
    /// </summary>
    public static void Register(ToolDefinition tool) => _tools.Add(tool);

    /// <summary>
    /// Associates a Segoe MDL2 Assets icon glyph with a category name.
    /// Called during startup alongside tool registration.
    /// </summary>
    public static void RegisterCategoryIcon(string category, string iconGlyph)
    {
        _categoryIcons[category] = iconGlyph;
    }

    /// <summary>
    /// Registers every tool's factory with the provided <paramref name="navigation"/> service,
    /// mapping each <see cref="ToolDefinition.Key"/> to its factory delegate.
    /// Call this after all tools have been <see cref="Register"/>ed.
    /// </summary>
    public static void RegisterAll(INavigationService navigation)
    {
        foreach (var tool in _tools)
            navigation.Register(tool.Key, tool.Factory);
    }

    /// <summary>
    /// Returns all tools belonging to the given category (case-insensitive).
    /// </summary>
    public static IEnumerable<ToolDefinition> GetByCategory(string category)
        => _tools.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds a list of <see cref="CategoryItem"/> objects from registered tools,
    /// preserving the order categories were first encountered. Excludes the "General"
    /// category (home) since it is not shown in the navigation bar.
    /// </summary>
    public static IReadOnlyList<CategoryItem> GetCategories()
    {
        var categories = new List<CategoryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in _tools)
        {
            if (tool.Category.Equals("General", StringComparison.OrdinalIgnoreCase))
                continue;

            if (seen.Add(tool.Category))
            {
                _categoryIcons.TryGetValue(tool.Category, out var icon);
                categories.Add(new CategoryItem
                {
                    Label = tool.Category,
                    Icon = icon ?? string.Empty,
                    Tools = _tools.Where(t => t.Category.Equals(tool.Category, StringComparison.OrdinalIgnoreCase)).ToList(),
                });
            }
        }

        return categories;
    }

    /// <summary>
    /// Returns all registered tools except those in the "General" category.
    /// Used by the home page to generate feature cards dynamically.
    /// </summary>
    public static IReadOnlyList<ToolDefinition> GetDisplayTools()
        => _tools.Where(t => !t.Category.Equals("General", StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Looks up a tool by its navigation key.
    /// Returns <c>null</c> if the key is not registered.
    /// </summary>
    public static ToolDefinition? GetByKey(string key)
        => _tools.FirstOrDefault(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
