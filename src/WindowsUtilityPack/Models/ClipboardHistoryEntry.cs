namespace WindowsUtilityPack.Models;

/// <summary>
/// One persisted clipboard history entry.
/// </summary>
public sealed class ClipboardHistoryEntry
{
    public long Id { get; init; }

    public DateTime CapturedUtc { get; init; }

    public string Content { get; init; } = string.Empty;

    public string ContentKind { get; init; } = "Text";
}