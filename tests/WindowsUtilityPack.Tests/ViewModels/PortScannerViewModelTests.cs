using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.NetworkInternet.PortScanner;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class PortScannerViewModelTests
{
    [Fact]
    public async Task Stop_WhileWaitingForAWorker_ReleasesBusyStateAndDrainsWorkers()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var vm = new PortScannerViewModel(new NullClipboardService(), new BackgroundTaskService(), async (_, _, _, ct) =>
        {
            Interlocked.Increment(ref active);
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); return false; }
            finally { Interlocked.Decrement(ref active); }
        }) { Host = "localhost", PortRangeText = "1-4", Concurrency = 1 };
        var scan = vm.RunScanAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        vm.StopCommand.Execute(null);
        await scan.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(vm.IsScanning);
        Assert.Equal(0, active);
        Assert.Contains("stopped", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.ScanCommand.CanExecute(null));
    }
    [Fact]
    public void Concurrency_IsCappedToSafeMaximum()
    {
        var vm = new PortScannerViewModel(new NullClipboardService(), new BackgroundTaskService())
        {
            Concurrency = 500,
        };

        Assert.Equal(PortScannerViewModel.MaxSafeConcurrency, vm.Concurrency);
    }

    [Fact]
    public void ParsePorts_SupportsRangesAndDeduplicates()
    {
        var ports = PortScannerViewModel.ParsePorts("22,80-82,80,65536,0, 443");

        Assert.Equal([22, 80, 81, 82, 443], ports);
    }

    private sealed class NullClipboardService : IClipboardService
    {
        public bool ContainsText() => false;

        public string GetText() => string.Empty;

        public void SetText(string text) { }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;

        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }
    }
}
