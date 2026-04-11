using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class BackgroundTaskServiceTests
{
    [Fact]
    public void BeginTask_CreatesRunningTask()
    {
        var service = new BackgroundTaskService();

        var taskId = service.BeginTask("Test task");
        var task = service.GetTasks().Single(t => t.TaskId == taskId);

        Assert.Equal(BackgroundTaskState.Running, task.State);
        Assert.Equal("Test task", task.Name);
    }

    [Fact]
    public void ReportProgress_UpdatesTaskProgress()
    {
        var service = new BackgroundTaskService();
        var taskId = service.BeginTask("Progress task");

        service.ReportProgress(taskId, new BackgroundTaskProgress
        {
            Percent = 42,
            Message = "Working",
            Detail = "Step 2",
        });

        var task = service.GetTasks().Single(t => t.TaskId == taskId);
        Assert.Equal(42, task.Percent);
        Assert.Equal("Working", task.Message);
        Assert.Equal("Step 2", task.Detail);
    }

    [Fact]
    public void CompleteTask_MovesTaskToFinishedHistory()
    {
        var service = new BackgroundTaskService();
        var taskId = service.BeginTask("Complete task");

        service.CompleteTask(taskId, "Done");

        var task = service.GetTasks().Single(t => t.TaskId == taskId);
        Assert.Equal(BackgroundTaskState.Completed, task.State);
        Assert.Equal(100, task.Percent);
        Assert.Equal("Done", task.Message);
        Assert.NotNull(task.FinishedUtc);
    }

    [Fact]
    public void CancelTask_CancelsTokenAndSetsCancelledState()
    {
        var service = new BackgroundTaskService();
        var taskId = service.BeginTask("Cancel task");
        var token = service.GetCancellationToken(taskId);

        var canceled = service.CancelTask(taskId, "User cancelled");

        Assert.True(canceled);
        Assert.True(token.IsCancellationRequested);

        var task = service.GetTasks().Single(t => t.TaskId == taskId);
        Assert.Equal(BackgroundTaskState.Cancelled, task.State);
        Assert.Equal("User cancelled", task.Message);
    }
}