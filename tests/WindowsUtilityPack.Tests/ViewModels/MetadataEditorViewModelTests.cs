using System.Threading.Tasks;
using WindowsUtilityPack.Tools.FileDataTools.MetadataEditor;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class MetadataEditorViewModelTests
{
    [Fact]
    public void FilePath_Set_UpdatesHasFile()
    {
        var vm = new MetadataEditorViewModel(new StubClipboard());
        vm.FilePath = "C:/test.jpg";
        Assert.True(vm.HasFile);
    }

    [Fact]
    public void CopyAllCommand_EmptyMetadata_NoCrash()
    {
        var vm = new MetadataEditorViewModel(new StubClipboard());
        vm.CopyAllCommand.Execute(null);
        Assert.True(true); // No exception
    }

    private sealed class StubClipboard : WindowsUtilityPack.Services.IClipboardService
    {
        public bool TryGetText(out string text) { text = string.Empty; return false; }
        public void SetText(string text) { }
        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
}