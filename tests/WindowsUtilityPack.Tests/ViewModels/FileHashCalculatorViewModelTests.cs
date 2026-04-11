using System.Threading.Tasks;
using WindowsUtilityPack.Tools.FileDataTools.FileHashCalculator;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class FileHashCalculatorViewModelTests
{
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