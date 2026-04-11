namespace WindowsUtilityPack.Models;

/// <summary>
/// Precomputed homepage view model for category cards.
/// Keeps count and usage-derived highlights out of XAML converters.
/// </summary>
public sealed class HomeCategorySummary
{
    /// <summary>The underlying category metadata from the tool registry.</summary>
    public required CategoryItem Category { get; init; }

    /// <summary>Total number of tools in the category.</summary>
    public int ToolCount { get; init; }

    /// <summary>Most frequently used tool label or a fallback when no usage exists yet.</summary>
    public string MostUsedToolName { get; init; } = string.Empty;

    /// <summary>Top tools previewed on hover in the category card.</summary>
    public IReadOnlyList<string> TopToolNames { get; init; } = [];
}
