using System.Threading.Tasks;
using WindowsUtilityPack.Tools.FileDataTools.FileHashCalculator;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class FileHashCalculatorViewModelTests
{
    [Fact]
    public async Task ComputeAsync_ProducesKnownHashAndChangingFileClearsIt()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllTextAsync(path, "abc");
            var vm = new FileHashCalculatorViewModel(new StubClipboard()) { FilePath = path };
            await vm.ComputeAsync();
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", vm.Sha256Hash);
            vm.VerifyHash = vm.Sha256Hash;
            vm.VerifyCommand.Execute(null);
            Assert.StartsWith("Match", vm.VerifyResult);
            vm.FilePath = path + ".missing";
            Assert.Empty(vm.Sha256Hash);
            Assert.Empty(vm.VerifyResult);
        }
        finally { System.IO.File.Delete(path); }
    }

    [Fact]
    public async Task ChangingFileDuringHashing_DiscardsOldResult()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllBytesAsync(path, new byte[16 * 1024 * 1024]);
            var vm = new FileHashCalculatorViewModel(new StubClipboard()) { FilePath = path };
            var operation = vm.ComputeAsync();
            vm.FilePath = path + ".missing";
            await operation;
            Assert.Empty(vm.Sha256Hash);
            Assert.False(vm.IsComputing);
            Assert.IsAssignableFrom<WindowsUtilityPack.ViewModels.IViewLifetime>(vm).Deactivate();
            Assert.IsAssignableFrom<IDisposable>(vm).Dispose();
        }
        finally { System.IO.File.Delete(path); }
    }

    [Fact]
    public void FilePath_Set_UpdatesHasFile()
    {
        var vm = new FileHashCalculatorViewModel(new StubClipboard());
        vm.FilePath = "C:/test.txt";
        Assert.True(vm.HasFile);
    }

    [Fact]
    public void VerifyHash_Set_UpdatesVerifyResult()
    {
        var vm = new FileHashCalculatorViewModel(new StubClipboard());
        vm.VerifyHash = "abc";
        Assert.Equal("abc", vm.VerifyHash);
    }

    private sealed class StubClipboard : WindowsUtilityPack.Services.IClipboardService
    {
        public bool TryGetText(out string text) { text = string.Empty; return false; }
        public void SetText(string text) { }
        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
}
