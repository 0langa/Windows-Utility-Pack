using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.SystemUtilities.ProcessExplorer;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class ProcessExplorerViewModelTests
{
    [Fact]
    public async Task RefreshAsync_LoadsRowsFromService()
    {
        using var vm = new ProcessExplorerViewModel(new StubService(), new StubDialogs(), new StubClipboard());

        await vm.RefreshAsync();

        Assert.Single(vm.Processes);
        Assert.Equal("Loaded 1 processes.", vm.StatusMessage);
    }

    [Fact]
    public async Task CopyDetailsAsync_CopiesToClipboard()
    {
        var clipboard = new StubClipboard();
        using var vm = new ProcessExplorerViewModel(new StubService(), new StubDialogs(), clipboard);

        await vm.RefreshAsync();
        vm.SelectedProcess = vm.Processes[0];

        await vm.CopyDetailsAsync();

        Assert.Equal("details", clipboard.LastText);
    }

    [Fact]
    public async Task RefreshAsync_PreservesSelectionForSameProcess()
    {
        using var vm = new ProcessExplorerViewModel(new StubService(), new StubDialogs(), new StubClipboard());
        vm.SelectedProcess = vm.Processes[0];
        await vm.RefreshAsync();
        Assert.Same(vm.Processes[0], vm.SelectedProcess);
    }

    [Fact]
    public async Task Dispose_PreventsFurtherRefreshes()
    {
        var service = new StubService();
        var vm = new ProcessExplorerViewModel(service, new StubDialogs(), new StubClipboard());
        var calls = service.Calls;
        vm.Dispose();
        await vm.RefreshAsync();
        Assert.Equal(calls, service.Calls);
    }

    private sealed class StubService : IProcessExplorerService
    {
        public int Calls { get; private set; }
        private readonly DateTime _started = DateTime.Now;
        public Task<string> BuildDetailsAsync(int processId, CancellationToken cancellationToken = default)
            => Task.FromResult("details");

        public Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(string? query = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            IReadOnlyList<ProcessSnapshot> rows =
            [
                new ProcessSnapshot
                {
                    ProcessId = 123,
                    Name = "TestProc",
                    ExecutablePath = "C:\\test.exe",
                    WorkingSetMb = 10,
                    CpuTimeSeconds = 1,
                    IsResponding = true,
                    StartTimeLocal = _started,
                },
            ];

            return Task.FromResult(rows);
        }

        public Task<bool> TryTerminateAsync(int processId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class StubDialogs : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;

        public void ShowError(string title, string message) { }

        public void ShowInfo(string title, string message) { }
    }

    private sealed class StubClipboard : IClipboardService
    {
        public string LastText { get; private set; } = string.Empty;

        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }

        public void SetText(string text)
        {
            LastText = text;
        }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
}
