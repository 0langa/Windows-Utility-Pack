using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Models;

public class ToolDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = "🔧";
    public required Func<ViewModelBase> Factory { get; init; }
}
