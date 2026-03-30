using System.Collections.ObjectModel;
using WindowsUtilityPack.Controls;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// Projection of a tool category for the shell navigation bar.
/// Built from <see cref="Tools.ToolRegistry"/> metadata at startup and exposed by
/// <see cref="MainWindowViewModel.Categories"/>.
/// </summary>
public sealed class CategoryNavItem
{
    /// <summary>Category display label shown on the navigation button.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Emoji icon shown above the label on the navigation button.</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>
    /// Tools belonging to this category, as <see cref="MenuEntry"/> items ready for
    /// <see cref="Controls.CategoryMenuButton.MenuItems"/>.
    /// </summary>
    public ObservableCollection<MenuEntry> MenuItems { get; init; } = [];
}
