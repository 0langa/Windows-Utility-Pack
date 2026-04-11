using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.SystemUtilities.TaskSchedulerUi;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class TaskSchedulerUiViewModelTests
{
    [Fact]
    public async Task RefreshAsync_LoadsTasks()
    {
        var vm = new TaskSchedulerUiViewModel(new StubService(), new StubDialogs());

        await vm.RefreshAsync();

        Assert.Single(vm.Tasks);
        Assert.Equal("Loaded 1 scheduled tasks.", vm.StatusMessage);
    }

    private sealed class StubService : ITaskSchedulerService
    {
        public Task<IReadOnlyList<ScheduledTaskRow>> GetTasksAsync(string? query = null, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ScheduledTaskRow> rows =
            [
                new ScheduledTaskRow
                {
                    TaskName = "\\Demo\\Task",
                    Status = "Ready",
                    NextRunTime = "N/A",
                    LastRunTime = "Never",
                    LastResult = "0",
                    Author = "User",
                },
            ];

            return Task.FromResult(rows);
        }

        public Task<bool> RunTaskAsync(string taskName, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class StubDialogs : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;

        public void ShowError(string title, string message) { }

        public void ShowInfo(string title, string message) { }
    }
}