namespace WindowsUtilityPack.Models;

/// <summary>
/// Kind of command surfaced in the command palette.
/// </summary>
public enum CommandPaletteItemKind
{
    Tool,
    ShellAction,
}

/// <summary>
/// A searchable command palette entry.
/// </summary>
public sealed class CommandPaletteItem
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string CommandKey { get; init; } = string.Empty;

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public CommandPaletteItemKind Kind { get; init; }
}