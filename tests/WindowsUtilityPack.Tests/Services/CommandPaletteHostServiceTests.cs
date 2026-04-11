using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class CommandPaletteHostServiceTests
{
    [Fact]
    public void ShowOrActivate_OpensWindowAndRaisesCommandInvoked()
    {
        var palette = new StubPaletteService();
        var hostWindow = new StubPaletteWindowHost();
        var service = new CommandPaletteHostService(palette, new StubFactory(hostWindow));
        CommandPaletteItem? invoked = null;
        service.CommandInvoked += (_, item) => invoked = item;

        service.ShowOrActivate();
        hostWindow.ViewModel!.RequestExecuteSelected();

        Assert.True(hostWindow.ShowCalled);
        Assert.NotNull(invoked);
        Assert.Equal("shell:settings", invoked!.Id);
        Assert.True(hostWindow.CloseCalled);
    }

    [Fact]
    public void ShowOrActivate_ActivatesExistingWindow()
    {
        var palette = new StubPaletteService();
        var hostWindow = new StubPaletteWindowHost { IsVisibleValue = true };
        var service = new CommandPaletteHostService(palette, new StubFactory(hostWindow));

        service.ShowOrActivate();
        service.ShowOrActivate();

        Assert.True(hostWindow.ActivateCalled);
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

    private sealed class StubFactory(StubPaletteWindowHost host) : ICommandPaletteWindowHostFactory
    {
        public ICommandPaletteWindowHost Create(CommandPaletteWindowViewModel viewModel)
        {
            host.ViewModel = viewModel;
            return host;
        }
    }

    private sealed class StubPaletteWindowHost : ICommandPaletteWindowHost
    {
        public event EventHandler? Closed;

        public bool IsVisible => IsVisibleValue;
        public bool IsVisibleValue { get; set; }

        public CommandPaletteWindowViewModel ViewModel { get; set; } = null!;

        public bool ShowCalled { get; private set; }
        public bool ActivateCalled { get; private set; }
        public bool CloseCalled { get; private set; }

        public void Show()
        {
            ShowCalled = true;
            IsVisibleValue = true;
        }

        public void Activate()
        {
            ActivateCalled = true;
        }

        public void Close()
        {
            CloseCalled = true;
            IsVisibleValue = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
