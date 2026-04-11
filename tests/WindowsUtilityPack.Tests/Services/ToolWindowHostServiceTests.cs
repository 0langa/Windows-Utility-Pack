using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.ViewModels;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class ToolWindowHostServiceTests
{
    [Fact]
    public void TryOpenOrActivate_OpensWindowForRegisteredTool()
    {
        var key = $"tool-window-{Guid.NewGuid():N}";
        ToolRegistry.Register(new ToolDefinition
        {
            Key = key,
            Name = "Windowed Tool",
            Category = "Test",
            Factory = () => new StubViewModel(),
        });

        var host = new FakeToolWindowHost();
        var service = new ToolWindowHostService(new FakeFactory(host));

        var result = service.TryOpenOrActivate(key, out _);

        Assert.True(result);
        Assert.Equal(1, host.ShowCalls);
        Assert.Equal(1, service.OpenWindowCount);
    }

    [Fact]
    public void TryOpenOrActivate_ActivatesExistingWindow()
    {
        var key = $"tool-window-{Guid.NewGuid():N}";
        ToolRegistry.Register(new ToolDefinition
        {
            Key = key,
            Name = "Windowed Tool",
            Category = "Test",
            Factory = () => new StubViewModel(),
        });

        var host = new FakeToolWindowHost();
        var service = new ToolWindowHostService(new FakeFactory(host));

        _ = service.TryOpenOrActivate(key, out _);
        _ = service.TryOpenOrActivate(key, out _);

        Assert.Equal(1, host.ShowCalls);
        Assert.Equal(1, host.ActivateCalls);
    }

    private sealed class StubViewModel : ViewModelBase { }

    private sealed class FakeFactory : IToolWindowHostFactory
    {
        private readonly IToolWindowHost _host;

        public FakeFactory(IToolWindowHost host)
        {
            _host = host;
        }

        public IToolWindowHost Create(ToolDefinition tool, object viewModel)
            => _host;
    }

    private sealed class FakeToolWindowHost : IToolWindowHost
    {
        public event EventHandler? Closed;

        public bool IsVisible { get; private set; }

        public int ShowCalls { get; private set; }

        public int ActivateCalls { get; private set; }

        public void Show()
        {
            ShowCalls++;
            IsVisible = true;
        }

        public void Activate()
        {
            ActivateCalls++;
        }

        public void Close()
        {
            IsVisible = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}