namespace WindowsUtilityPack.Models;

/// <summary>
/// Represents one persisted activity event emitted by the application.
/// </summary>
public sealed class ActivityLogEntry
{
    public long Id { get; init; }

    public DateTime TimestampUtc { get; init; }

    public required string Category { get; init; }

    public required string Action { get; init; }

    public string Details { get; init; } = string.Empty;

    public bool IsSensitive { get; init; }
}