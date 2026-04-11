using System.Collections.Concurrent;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Default in-memory tracker for cancellable background work.
/// </summary>
public sealed class BackgroundTaskService : IBackgroundTaskService
{
    private const int MaxFinishedHistory = 200;

    private readonly ConcurrentDictionary<Guid, TaskEnvelope> _active = new();
    private readonly Queue<BackgroundTaskInfo> _finished = new();
    private readonly object _historyLock = new();

    public event EventHandler<BackgroundTaskInfo>? TaskChanged;

    /// <inheritdoc />
    public Guid BeginTask(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name must be provided.", nameof(name));
        }

        var id = Guid.NewGuid();
        var info = new BackgroundTaskInfo
        {
            TaskId = id,
            Name = name,
            State = BackgroundTaskState.Running,
            StartedUtc = DateTime.UtcNow,
            Percent = 0,
            Message = "Running",
        };

        var envelope = new TaskEnvelope(info, new CancellationTokenSource());
        _active[id] = envelope;
        TaskChanged?.Invoke(this, info);
        return id;
    }

    /// <inheritdoc />
    public CancellationToken GetCancellationToken(Guid taskId)
    {
        if (!_active.TryGetValue(taskId, out var envelope))
        {
            throw new InvalidOperationException($"Task {taskId} is not active.");
        }

        return envelope.Cts.Token;
    }

    /// <inheritdoc />
    public void ReportProgress(Guid taskId, BackgroundTaskProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        if (!_active.TryGetValue(taskId, out var envelope))
        {
            return;
        }

        var current = envelope.Info;
        var updated = new BackgroundTaskInfo
        {
            TaskId = current.TaskId,
            Name = current.Name,
            State = BackgroundTaskState.Running,
            StartedUtc = current.StartedUtc,
            Percent = Math.Clamp(progress.Percent, 0, 100),
            Message = string.IsNullOrWhiteSpace(progress.Message) ? current.Message : progress.Message,
            Detail = progress.Detail ?? string.Empty,
        };

        envelope.Info = updated;
        TaskChanged?.Invoke(this, updated);
    }

    /// <inheritdoc />
    public void CompleteTask(Guid taskId, string? message = null)
    {
        FinishTask(taskId, BackgroundTaskState.Completed, message: message ?? "Completed");
    }

    /// <inheritdoc />
    public void FailTask(Guid taskId, Exception exception, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        FinishTask(taskId, BackgroundTaskState.Failed, message: message ?? "Failed", error: exception.Message);
    }

    /// <inheritdoc />
    public bool CancelTask(Guid taskId, string? message = null)
    {
        if (!_active.TryGetValue(taskId, out var envelope))
        {
            return false;
        }

        envelope.Cts.Cancel();
        FinishTask(taskId, BackgroundTaskState.Cancelled, message: message ?? "Cancelled");
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<BackgroundTaskInfo> GetTasks()
    {
        var running = _active.Values.Select(v => v.Info).OrderByDescending(v => v.StartedUtc).ToList();
        List<BackgroundTaskInfo> finished;
        lock (_historyLock)
        {
            finished = _finished.OrderByDescending(f => f.FinishedUtc).ToList();
        }

        return running.Concat(finished).ToList();
    }

    private void FinishTask(Guid taskId, BackgroundTaskState finalState, string message, string error = "")
    {
        if (!_active.TryRemove(taskId, out var envelope))
        {
            return;
        }

        try
        {
            envelope.Cts.Dispose();
        }
        catch
        {
            // Best-effort dispose.
        }

        var current = envelope.Info;
        var finished = new BackgroundTaskInfo
        {
            TaskId = current.TaskId,
            Name = current.Name,
            State = finalState,
            StartedUtc = current.StartedUtc,
            FinishedUtc = DateTime.UtcNow,
            Percent = finalState == BackgroundTaskState.Completed ? 100 : current.Percent,
            Message = message,
            Detail = current.Detail,
            Error = error,
        };

        lock (_historyLock)
        {
            _finished.Enqueue(finished);
            while (_finished.Count > MaxFinishedHistory)
            {
                _finished.Dequeue();
            }
        }

        TaskChanged?.Invoke(this, finished);
    }

    private sealed class TaskEnvelope
    {
        public TaskEnvelope(BackgroundTaskInfo info, CancellationTokenSource cts)
        {
            Info = info;
            Cts = cts;
        }

        public BackgroundTaskInfo Info { get; set; }

        public CancellationTokenSource Cts { get; }
    }
}