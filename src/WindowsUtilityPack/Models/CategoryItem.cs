namespace WindowsUtilityPack.Models;

/// <summary>
/// Represents a top-level navigation category with its sub-items.
/// </summary>
public class CategoryItem
{
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public IReadOnlyList<string> SubItems { get; set; } = [];
}
