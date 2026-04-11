using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.NetworkInternet.PortScanner;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class PortScannerViewModelTests
{
    [Fact]
    public void Concurrency_IsCappedToSafeMaximum()
    {
        var vm = new PortScannerViewModel(new NullClipboardService())
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
