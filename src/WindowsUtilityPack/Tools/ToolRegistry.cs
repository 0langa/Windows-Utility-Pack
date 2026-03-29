using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack.Tools;

/// <summary>
/// Central registry of all available tools.
/// To add a new tool: add a ToolDefinition here and register it with the navigation service.
/// Future: load from plugin assemblies via MEF or similar mechanism.
/// </summary>
public static class ToolRegistry
{
    private static readonly List<ToolDefinition> _tools = [];

    public static IReadOnlyList<ToolDefinition> All => _tools;

    public static void Register(ToolDefinition tool) => _tools.Add(tool);

    public static void RegisterAll(INavigationService navigation)
    {
        foreach (var tool in _tools)
            navigation.Register(tool.Key, tool.Factory);
    }

    public static IEnumerable<ToolDefinition> GetByCategory(string category)
        => _tools.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
}
