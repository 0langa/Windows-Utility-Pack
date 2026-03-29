using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Models;

/// <summary>
/// Metadata and factory for a single tool registered in the application.
/// Each <see cref="ToolDefinition"/> is added to the <c>ToolRegistry</c> at startup
/// and maps to exactly one ViewModel type (and, through WPF DataTemplates, one View).
/// </summary>
public class ToolDefinition
{
    /// <summary>
    /// Unique identifier used for navigation (e.g. <c>"disk-info"</c>).
    /// Must match the <c>ToolKey</c> used in <c>MenuEntry</c> elements in <c>MainWindow.xaml</c>.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>Human-readable display name shown in the UI.</summary>
    public required string Name { get; init; }

    /// <summary>Category name (e.g. "System Utilities").  Used by <c>ToolRegistry.GetByCategory</c>.</summary>
    public required string Category { get; init; }

    /// <summary>Short description of the tool's purpose.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Emoji or icon string used in the navigation UI.</summary>
    public string Icon { get; init; } = "🔧";

    /// <summary>
    /// Factory that creates a fresh ViewModel instance every time the tool is navigated to.
    /// Using a factory (rather than a singleton) ensures the tool always starts with clean state.
    /// </summary>
    public required Func<ViewModelBase> Factory { get; init; }
}
