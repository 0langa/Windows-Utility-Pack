using System.Linq;
using System.Threading.Tasks;
using WindowsUtilityPack.Tools.FileDataTools.SecureFileShredder;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class SecureFileShredderViewModelTests
{
    [Fact]
    public void OverwriteAndDelete_DeletesOnlyTheSelectedFixture()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllBytes(path, new byte[131073]);
            WindowsUtilityPack.Services.FileTools.FileOverwriteService.OverwriteAndDelete(path, 1);
            Assert.False(System.IO.File.Exists(path));
        }
        finally { System.IO.File.Delete(path); }
    }

    [Fact]
    public void OverwriteAndDelete_CanceledBeforeStartPreservesContent()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(path, "keep");
            Assert.Throws<OperationCanceledException>(() => WindowsUtilityPack.Services.FileTools.FileOverwriteService
                .OverwriteAndDelete(path, 1, new System.Threading.CancellationToken(true)));
            Assert.Equal("keep", System.IO.File.ReadAllText(path));
        }
        finally { System.IO.File.Delete(path); }
    }
    [Fact]
    public void ShredFile_WriteFailureDoesNotHideOriginalUnderRandomName()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shred-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, "fixture.txt");
        System.IO.File.WriteAllText(path, "owned disposable fixture");
        try
        {
            using (var locked = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read,
                System.IO.FileShare.Read | System.IO.FileShare.Delete))
            {
                var method = typeof(SecureFileShredderViewModel).GetMethod("ShredFile",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
                Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null, [path, 1]));
                Assert.True(System.IO.File.Exists(path), "A failed overwrite must keep the known original path.");
            }
        }
        finally { System.IO.Directory.Delete(folder, true); }
    }

    [Fact]
    public void ShredFile_MissingInputIsReportedAsFailure()
    {
        var method = typeof(SecureFileShredderViewModel).GetMethod("ShredFile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null,
            [System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".missing"), 1]));
        Assert.IsType<System.IO.FileNotFoundException>(error.InnerException);
    }

    [Fact]
    public void AddFilesCommand_QueuesFiles()
    {
        var vm = new SecureFileShredderViewModel(new StubDialog());
        // Simulate adding files (bypass dialog)
        vm.Files.Add(new ShredderFileEntry { FilePath = "C:/test1.txt", FileName = "test1.txt", SizeDisplay = "1 KB" });
        vm.Files.Add(new ShredderFileEntry { FilePath = "C:/test2.txt", FileName = "test2.txt", SizeDisplay = "2 KB" });
        Assert.Equal(2, vm.Files.Count);
    }

    [Fact]
    public void RemoveSelectedCommand_RemovesFile()
    {
        var vm = new SecureFileShredderViewModel(new StubDialog());
        var entry = new ShredderFileEntry { FilePath = "C:/test.txt", FileName = "test.txt", SizeDisplay = "1 KB" };
        vm.Files.Add(entry);
        vm.SelectedFile = entry;
        vm.RemoveSelectedCommand.Execute(null);
        Assert.Empty(vm.Files);
    }

    [Fact]
    public void ClearAllCommand_ClearsFiles()
    {
        var vm = new SecureFileShredderViewModel(new StubDialog());
        vm.Files.Add(new ShredderFileEntry { FilePath = "C:/test.txt", FileName = "test.txt", SizeDisplay = "1 KB" });
        vm.ClearAllCommand.Execute(null);
        Assert.Empty(vm.Files);
    }

    private sealed class StubDialog : WindowsUtilityPack.Services.IUserDialogService
    {
        public bool Confirm(string title, string message) => true;
        public void ShowError(string title, string message) { }
        public void ShowInfo(string title, string message) { }
    }
}
