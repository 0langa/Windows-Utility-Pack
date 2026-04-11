using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Tracks cancellable background tasks and publishes progress snapshots.
/// </summary>
public interface IBackgroundTaskService
{
    /// <summary>
    /// Raised whenever a task state or progress changes.
    /// </summary>
    event EventHandler<BackgroundTaskInfo>? TaskChanged;

    /// <summary>
    /// Registers a new task and returns its identifier.
    /// </summary>
    Guid BeginTask(string name);

    /// <summary>
    /// Returns cancellation token associated with a running task.
    /// </summary>
    CancellationToken GetCancellationToken(Guid taskId);

    /// <summary>
    /// Reports task progress update.
    /// </summary>
    void ReportProgress(Guid taskId, BackgroundTaskProgress progress);

    /// <summary>
    /// Marks task as completed.
    /// </summary>
    void CompleteTask(Guid taskId, string? message = null);

    /// <summary>
    /// Marks task as failed.
    /// </summary>
    void FailTask(Guid taskId, Exception exception, string? message = null);

    /// <summary>
    /// Requests cancellation for a running task.
    /// </summary>
    bool CancelTask(Guid taskId, string? message = null);

    /// <summary>
    /// Returns tracked active and recently finished tasks.
    /// </summary>
    IReadOnlyList<BackgroundTaskInfo> GetTasks();
}