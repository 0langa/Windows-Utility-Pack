namespace WindowsUtilityPack.Models;

/// <summary>
/// Lifecycle state for a tracked background task.
/// </summary>
public enum BackgroundTaskState
{
    Running,
    Completed,
    Cancelled,
    Failed,
}

/// <summary>
/// Progress payload emitted by background task workers.
/// </summary>
public sealed class BackgroundTaskProgress
{
    public int Percent { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// Snapshot describing a tracked background task.
/// </summary>
public sealed class BackgroundTaskInfo
{
    public Guid TaskId { get; init; }

    public string Name { get; init; } = string.Empty;

    public BackgroundTaskState State { get; init; }

    public DateTime StartedUtc { get; init; }

    public DateTime? FinishedUtc { get; init; }

    public int Percent { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string Error { get; init; } = string.Empty;
}