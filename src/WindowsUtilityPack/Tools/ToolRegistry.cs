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
///   <item>The UI (home page cards, category menu entries) uses these keys as
///         <c>CommandParameter</c> / <c>ToolKey</c> values.</item>
/// </list>
///
/// Future extension:
/// The registry is deliberately simple so it can be extended to load tools from
/// external plugin assemblies (e.g., via MEF / <c>AssemblyLoadContext</c>) without
/// changing any call sites.  Simply populate <c>_tools</c> from discovered assemblies
/// before calling <see cref="RegisterAll"/>.
/// </summary>
public static class ToolRegistry
{
    private static readonly List<ToolDefinition> _tools = [];

    /// <summary>All registered tool definitions, in registration order.</summary>
    public static IReadOnlyList<ToolDefinition> All => _tools;

    /// <summary>
    /// Adds a tool to the registry.
    /// Called once per tool in <c>App.xaml.cs RegisterTools()</c>.
    /// </summary>
    public static void Register(ToolDefinition tool) => _tools.Add(tool);

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
    /// Useful for dynamically generating UI from the registry.
    /// </summary>
    public static IEnumerable<ToolDefinition> GetByCategory(string category)
        => _tools.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
}
