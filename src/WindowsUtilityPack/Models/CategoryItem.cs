namespace WindowsUtilityPack.Models;

/// <summary>
/// Represents a top-level navigation category with its display name, icon, and tool sub-items.
/// Built dynamically from <see cref="ToolRegistry"/> metadata so the shell and home page
/// are driven by a single source of truth.
/// </summary>
public class CategoryItem
{
    /// <summary>Display label shown in the navigation bar.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Segoe MDL2 Assets glyph character shown above the label.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Short summary of what tools belong to this category.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Tool definitions belonging to this category.</summary>
    public IReadOnlyList<ToolDefinition> Tools { get; set; } = [];
}
