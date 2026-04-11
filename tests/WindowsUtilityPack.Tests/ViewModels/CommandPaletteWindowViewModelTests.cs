using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public sealed class CommandPaletteWindowViewModelTests
{
    [Fact]
    public void QueryRefreshesItems()
    {
        var service = new StubPaletteService();
        var vm = new CommandPaletteWindowViewModel(service);

        vm.Query = "settings";

        Assert.NotEmpty(vm.Items);
        Assert.Equal("Open Settings", vm.Items[0].Title);
    }

    [Fact]
    public void RequestExecuteSelected_RaisesEvent()
    {
        var service = new StubPaletteService();
        var vm = new CommandPaletteWindowViewModel(service);
        CommandPaletteItem? invoked = null;
        vm.ExecuteRequested += (_, item) => invoked = item;

        vm.RequestExecuteSelected();

        Assert.NotNull(invoked);
        Assert.Equal("shell:settings", invoked!.Id);
    }

    [Fact]
    public void ActivateFresh_ClearsQuery_AndRestoresFirstSelection()
    {
        var service = new StubPaletteService();
        var vm = new CommandPaletteWindowViewModel(service)
        {
            Query = "settings"
        };

        vm.ActivateFresh();

        Assert.Equal(string.Empty, vm.Query);
        Assert.NotNull(vm.SelectedItem);
        Assert.Equal("shell:settings", vm.SelectedItem!.Id);
    }

    [Fact]
    public void ExecuteSelectedCommand_CanExecute_TracksSelectionState()
    {
        var service = new StubPaletteService();
        var vm = new CommandPaletteWindowViewModel(service);

        Assert.True(vm.ExecuteSelectedCommand.CanExecute(null));

        vm.SelectedItem = null;

        Assert.False(vm.ExecuteSelectedCommand.CanExecute(null));
    }

    private sealed class StubPaletteService : ICommandPaletteService
    {
        public IReadOnlyList<CommandPaletteItem> Search(string? query, int limit = 20)
        {
            return
            [
                new CommandPaletteItem
                {
                    Id = "shell:settings",
                    Title = "Open Settings",
                    Subtitle = "Settings",
                    Category = "Shell",
                    CommandKey = "open-settings",
                    Kind = CommandPaletteItemKind.ShellAction,
                }
            ];
        }

        public void RecordExecution(string itemId)
        {
        }
    }
}
