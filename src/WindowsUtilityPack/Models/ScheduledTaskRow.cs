namespace WindowsUtilityPack.Models;

/// <summary>
/// Represents one scheduled task entry for Task Scheduler UI.
/// </summary>
public sealed class ScheduledTaskRow
{
    public string TaskName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string NextRunTime { get; init; } = string.Empty;

    public string LastRunTime { get; init; } = string.Empty;

    public string LastResult { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;
}