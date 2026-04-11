using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class TrayModeCoordinatorTests
{
    [Fact]
    public void ShouldHideOnMinimize_ReturnsTrue_WhenTrayAndMinimizeEnabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings = new AppSettings { TrayModeEnabled = true, MinimizeToTray = true };

        Assert.True(coordinator.ShouldHideOnMinimize(settings));
    }

    [Fact]
    public void ShouldInterceptClose_ReturnsFalse_WhenExplicitExitRequested()
    {
        var coordinator = new TrayModeCoordinator();
        var settings = new AppSettings { TrayModeEnabled = true, CloseToTray = true };

        Assert.False(coordinator.ShouldInterceptClose(settings, explicitExitRequested: true));
    }

    [Fact]
    public void ShouldInterceptClose_ReturnsFalse_WhenCloseToTrayDisabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings = new AppSettings { TrayModeEnabled = true, CloseToTray = false };

        Assert.False(coordinator.ShouldInterceptClose(settings, explicitExitRequested: false));
    }

    [Fact]
    public void ShouldStartMinimizedToTray_ReturnsTrue_WhenConfigured()
    {
        var coordinator = new TrayModeCoordinator();
        var settings = new AppSettings { TrayModeEnabled = true, StartMinimizedToTray = true };

        Assert.True(coordinator.ShouldStartMinimizedToTray(settings));
    }

    [Fact]
    public void ShouldShowTaskCompletionAlert_ReturnsTrue_ForCompletedTask()
    {
        var coordinator = new TrayModeCoordinator();
        var task = new BackgroundTaskInfo { Name = "Scan", State = BackgroundTaskState.Completed };

        Assert.True(coordinator.ShouldShowTaskCompletionAlert(task));
    }

    [Fact]
    public void BuildTaskAlertMessage_UsesTaskMessage_WhenPresent()
    {
        var coordinator = new TrayModeCoordinator();
        var task = new BackgroundTaskInfo
        {
            Name = "Download",
            State = BackgroundTaskState.Completed,
            Message = "Completed",
        };

        var result = coordinator.BuildTaskAlertMessage(task);

        Assert.Contains("Download", result);
        Assert.Contains("Completed", result);
    }
}
