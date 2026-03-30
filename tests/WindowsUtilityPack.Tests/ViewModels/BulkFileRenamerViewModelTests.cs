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
