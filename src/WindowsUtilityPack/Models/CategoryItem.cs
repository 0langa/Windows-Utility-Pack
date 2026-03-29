namespace WindowsUtilityPack.Models;

/// <summary>
/// Represents a top-level navigation category with its display name, icon, and sub-items.
/// Currently defined as a data holder for future use (e.g., dynamically building the nav bar
/// from a collection rather than hard-coding it in <c>MainWindow.xaml</c>).
/// </summary>
public class CategoryItem
{
    /// <summary>Display label shown in the navigation bar.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Emoji or icon character shown above the label.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Names of the sub-tools listed in the hover dropdown for this category.</summary>
    public IReadOnlyList<string> SubItems { get; set; } = [];
}
