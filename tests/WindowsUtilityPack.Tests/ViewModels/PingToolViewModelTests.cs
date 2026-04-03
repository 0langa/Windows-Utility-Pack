using WindowsUtilityPack.Tools.NetworkInternet.PingTool;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="PingToolViewModel"/>.
/// Verifies initial state, property clamping, and command availability.
/// Network-dependent tests (actual pinging) are intentionally excluded.
/// </summary>
public class PingToolViewModelTests
{
    [Fact]
    public void DefaultHost_IsNotEmpty()
    {
        var vm = new PingToolViewModel();
        Assert.False(string.IsNullOrWhiteSpace(vm.Host));
    }

    [Fact]
    public void PingCount_ClampedToMinimum()
    {
        var vm = new PingToolViewModel { PingCount = 0 };
        Assert.Equal(1, vm.PingCount);
    }

    [Fact]
    public void PingCount_ClampedToMaximum()
    {
        var vm = new PingToolViewModel { PingCount = 100 };
        Assert.Equal(20, vm.PingCount);
    }

    [Fact]
    public void IsPinging_FalseInitially()
    {
        var vm = new PingToolViewModel();
        Assert.False(vm.IsPinging);
    }

    [Fact]
    public void Summary_EmptyInitially()
    {
        var vm = new PingToolViewModel();
        Assert.Equal(string.Empty, vm.Summary);
    }

    [Fact]
    public void PingCommand_CanExecute_WhenNotPinging()
    {
        var vm = new PingToolViewModel();
        Assert.True(vm.PingCommand.CanExecute(null));
    }

    [Fact]
    public void StopCommand_CannotExecute_WhenNotPinging()
    {
        var vm = new PingToolViewModel();
        Assert.False(vm.StopCommand.CanExecute(null));
    }

    [Fact]
    public void Results_EmptyInitially()
    {
        var vm = new PingToolViewModel();
        Assert.Empty(vm.Results);
    }
}
