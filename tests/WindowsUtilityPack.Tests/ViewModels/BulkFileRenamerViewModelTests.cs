using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.FileDataTools.BulkFileRenamer;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="BulkFileRenamerViewModel"/>.
/// Verifies service-call behaviour using simple test doubles for
/// <see cref="IFolderPickerService"/> and <see cref="IUserDialogService"/>.
/// </summary>
public class BulkFileRenamerViewModelTests
{
    [Theory]
    [InlineData("CON")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("bad:name")]
    public void Preview_RejectsInvalidWindowsNames(string replacement)
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rename-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "input.txt"), "keep");
            var vm = new BulkFileRenamerViewModel(new StubFolderPickerService(null), new RecordingDialogs())
                { SelectedFolder = folder, FindText = "input", ReplaceText = replacement };
            Assert.True(Assert.Single(vm.PreviewItems).HasConflict);
        }
        finally { System.IO.Directory.Delete(folder, true); }
    }

    [Fact]
    public void ApplyRename_LaterFailureRestoresEarlierRenames()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rename-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        try
        {
            var first = System.IO.Path.Combine(folder, "a.txt");
            var second = System.IO.Path.Combine(folder, "b.txt");
            System.IO.File.WriteAllText(first, "first");
            System.IO.File.WriteAllText(second, "second");
            var dialogs = new RecordingDialogs();
            var vm = new BulkFileRenamerViewModel(new StubFolderPickerService(null), dialogs) { SelectedFolder = folder };
            vm.PreviewItems.Clear();
            vm.PreviewItems.Add(new() { OriginalName = "a.txt", NewName = "renamed-a.txt" });
            vm.PreviewItems.Add(new() { OriginalName = "b.txt", NewName = "renamed-b.txt" });
            using (var locked = new System.IO.FileStream(second, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None))
                vm.ApplyRenameCommand.Execute(null);
            Assert.Equal("first", System.IO.File.ReadAllText(first));
            Assert.Equal("second", System.IO.File.ReadAllText(second));
            Assert.NotEmpty(dialogs.Errors);
            Assert.False(vm.IsBusy);
        }
        finally { System.IO.Directory.Delete(folder, true); }
    }

    private sealed class RecordingDialogs : IUserDialogService
    {
        public List<string> Errors { get; } = [];
        public bool Confirm(string title, string message) => true;
        public void ShowInfo(string title, string message) { }
        public void ShowError(string title, string message) => Errors.Add(message);
    }

    [Fact]
    public void BrowseFolderCommand_SetsSelectedFolder_WhenPickerReturnsPath()
    {
        var picker  = new StubFolderPickerService(@"C:\TestFolder");
        var dialogs = new NullDialogService();
        var vm      = new BulkFileRenamerViewModel(picker, dialogs);

        vm.BrowseFolderCommand.Execute(null);

        Assert.Equal(@"C:\TestFolder", vm.SelectedFolder);
    }

    [Fact]
    public void BrowseFolderCommand_DoesNotChangeSelectedFolder_WhenPickerReturnsCancelled()
    {
        var picker  = new StubFolderPickerService(null);
        var dialogs = new NullDialogService();
        var vm      = new BulkFileRenamerViewModel(picker, dialogs);
        vm.SelectedFolder = @"C:\Original";

        vm.BrowseFolderCommand.Execute(null);

        Assert.Equal(@"C:\Original", vm.SelectedFolder);
    }

    [Fact]
    public void ApplyRenameCommand_IsDisabled_WhenPreviewIsEmpty()
    {
        var vm = new BulkFileRenamerViewModel(new StubFolderPickerService(null), new NullDialogService());

        Assert.False(vm.ApplyRenameCommand.CanExecute(null));
    }

    [Fact]
    public void IsBusy_IsFalse_Initially()
    {
        var vm = new BulkFileRenamerViewModel(new StubFolderPickerService(null), new NullDialogService());

        Assert.False(vm.IsBusy);
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class StubFolderPickerService(string? path) : IFolderPickerService
    {
        public string? PickFolder(string title) => path;
    }

    private sealed class NullDialogService : IUserDialogService
    {
        public bool Confirm(string title, string message)  => false;
        public void ShowInfo(string title, string message) { }
        public void ShowError(string title, string message) { }
    }
}
