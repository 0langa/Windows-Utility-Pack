using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.SystemUtilities.BackgroundTaskMonitor;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class BackgroundTaskMonitorViewModelTests
{
    [Fact]
    public void Refresh_ShowsTrackedTasks()
    {
        var tasks = new BackgroundTaskService();
        _ = tasks.BeginTask("Sample task");

        var vm = new BackgroundTaskMonitorViewModel(tasks);

        Assert.NotEmpty(vm.TaskItems);
    }

    [Fact]
    public void CancelSelected_CancelsRunningTask()
    {
        var tasks = new BackgroundTaskService();
        var taskId = tasks.BeginTask("Cancelable task");

        var vm = new BackgroundTaskMonitorViewModel(tasks);
        vm.SelectedTask = vm.TaskItems.FirstOrDefault(t => t.TaskId == taskId);

        vm.CancelSelectedCommand.Execute(null);

        Assert.Contains(vm.TaskItems, t => t.TaskId == taskId && t.State == WindowsUtilityPack.Models.BackgroundTaskState.Cancelled);
    }
}