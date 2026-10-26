using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class AutomationActionDispatcherTests
{
    [Theory]
    [InlineData("editor")]
    [InlineData("EDITOR.exe")]
    public async Task KillProcess_OnlyDispatchesExactNamesWithKnownIdentity(string target)
    {
        var processes = new ProcessFixture();
        var dispatcher = new AutomationActionDispatcher(processes, (_, _) => Task.CompletedTask);
        await dispatcher.DispatchAsync(new AutomationRule { ActionType = AutomationActionType.KillProcess, ActionTarget = target }, CancellationToken.None);
        Assert.Equal(new[] { 100 }, processes.Terminated);
    }

    [Fact]
    public async Task RunCleanup_OpensReviewWithoutDestructiveProcessActions()
    {
        var processes = new ProcessFixture();
        string? opened = null;
        var dispatcher = new AutomationActionDispatcher(processes, (key, _) => { opened = key; return Task.CompletedTask; });
        await dispatcher.DispatchAsync(new AutomationRule { ActionType = AutomationActionType.RunCleanup }, CancellationToken.None);
        Assert.Equal("storage-master", opened);
        Assert.Empty(processes.Terminated);
    }

    private sealed class ProcessFixture : IProcessExplorerService
    {
        public List<int> Terminated { get; } = [];
        public Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(string? query = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessSnapshot>>([
                new() { ProcessId = 100, Name = "editor", StartTimeLocal = DateTime.Today },
                new() { ProcessId = 101, Name = "editor-helper", StartTimeLocal = DateTime.Today },
                new() { ProcessId = 102, Name = "editor" },
            ]);
        public Task<string> BuildDetailsAsync(int processId, CancellationToken cancellationToken = default) => Task.FromResult("");
        public Task<bool> TryTerminateAsync(int processId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Termination must revalidate the selected process identity.");
        public Task<bool> TryTerminateAsync(ProcessSnapshot process, CancellationToken cancellationToken = default)
        {
            Terminated.Add(process.ProcessId);
            return Task.FromResult(true);
        }
    }
}
