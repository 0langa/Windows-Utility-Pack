namespace WindowsUtilityPack.Models;

/// <summary>
/// A persisted profile describing a reusable workspace context.
/// </summary>
public sealed class WorkspaceProfile
{
    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public string StartupToolKey { get; init; } = "home";

    public IReadOnlyList<string> PinnedToolKeys { get; init; } = [];

    public DateTime CreatedUtc { get; init; }

    public DateTime UpdatedUtc { get; init; }
}