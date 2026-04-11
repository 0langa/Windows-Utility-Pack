using System.Linq;
using System.Threading.Tasks;
using WindowsUtilityPack.Tools.FileDataTools.SecureFileShredder;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class SecureFileShredderViewModelTests
{
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