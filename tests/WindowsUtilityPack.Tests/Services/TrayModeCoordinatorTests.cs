using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class TrayModeCoordinatorTests
{
    // ── ShouldHideOnMinimize ──────────────────────────────────────────────────

    [Fact]
    public void ShouldHideOnMinimize_ReturnsTrue_WhenTrayAndMinimizeEnabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = true, MinimizeToTray = true };

        Assert.True(coordinator.ShouldHideOnMinimize(settings));
    }

    [Fact]
    public void ShouldHideOnMinimize_ReturnsFalse_WhenTrayDisabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = false, MinimizeToTray = true };

        Assert.False(coordinator.ShouldHideOnMinimize(settings));
    }

    [Fact]
    public void ShouldHideOnMinimize_ReturnsFalse_WhenMinimizeToTrayDisabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = true, MinimizeToTray = false };

        Assert.False(coordinator.ShouldHideOnMinimize(settings));
    }

    [Fact]
    public void ShouldHideOnMinimize_ReturnsFalse_WhenBothDisabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = false, MinimizeToTray = false };

        Assert.False(coordinator.ShouldHideOnMinimize(settings));
    }

    // ── ShouldInterceptClose ──────────────────────────────────────────────────

    [Fact]
    public void ShouldInterceptClose_ReturnsTrue_WhenTrayEnabledAndNoExplicitExit()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = true };

        Assert.True(coordinator.ShouldInterceptClose(settings, explicitExitRequested: false));
    }

    [Fact]
    public void ShouldInterceptClose_ReturnsFalse_WhenExplicitExitRequested()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = true, CloseToTray = true };

        Assert.False(coordinator.ShouldInterceptClose(settings, explicitExitRequested: true));
    }

    [Fact]
    public void ShouldInterceptClose_ReturnsFalse_WhenTrayModeDisabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = false, CloseToTray = true };

        Assert.False(coordinator.ShouldInterceptClose(settings, explicitExitRequested: false));
    }

    [Fact]
    public void ShouldInterceptClose_ReturnsFalse_WhenCloseToTrayDisabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = true, CloseToTray = false };

        Assert.False(coordinator.ShouldInterceptClose(settings, explicitExitRequested: false));
    }

    // ── ShouldShowTrayAlert ───────────────────────────────────────────────────

    [Fact]
    public void ShouldShowTrayAlert_ReturnsTrue_WhenTrayEnabledAlertsEnabledAndWindowHidden()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = true, TrayAlertsEnabled = true };

        Assert.True(coordinator.ShouldShowTrayAlert(settings, isWindowHidden: true));
    }

    [Fact]
    public void ShouldShowTrayAlert_ReturnsFalse_WhenWindowNotHidden()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = true, TrayAlertsEnabled = true };

        Assert.False(coordinator.ShouldShowTrayAlert(settings, isWindowHidden: false));
    }

    [Fact]
    public void ShouldShowTrayAlert_ReturnsFalse_WhenAlertsDisabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = true, TrayAlertsEnabled = false };

        Assert.False(coordinator.ShouldShowTrayAlert(settings, isWindowHidden: true));
    }

    [Fact]
    public void ShouldShowTrayAlert_ReturnsFalse_WhenTrayModeDisabled()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = false, TrayAlertsEnabled = true };

        Assert.False(coordinator.ShouldShowTrayAlert(settings, isWindowHidden: true));
    }

    // ── ShouldStartMinimizedToTray ────────────────────────────────────────────

    [Fact]
    public void ShouldStartMinimizedToTray_ReturnsTrue_WhenConfigured()
    {
        var coordinator = new TrayModeCoordinator();
        var settings    = new AppSettings { TrayModeEnabled = true, StartMinimizedToTray = true };

        Assert.True(coordinator.ShouldStartMinimizedToTray(settings));
    }

    // ── ShouldShowTaskCompletionAlert ─────────────────────────────────────────

    [Fact]
    public void ShouldShowTaskCompletionAlert_ReturnsTrue_ForCompletedTask()
    {
        var coordinator = new TrayModeCoordinator();
        var task        = new BackgroundTaskInfo { Name = "Scan", State = BackgroundTaskState.Completed };

        Assert.True(coordinator.ShouldShowTaskCompletionAlert(task));
    }

    [Fact]
    public void ShouldShowTaskCompletionAlert_ReturnsTrue_ForFailedTask()
    {
        var coordinator = new TrayModeCoordinator();
        var task        = new BackgroundTaskInfo { Name = "Download", State = BackgroundTaskState.Failed };

        Assert.True(coordinator.ShouldShowTaskCompletionAlert(task));
    }

    [Fact]
    public void ShouldShowTaskCompletionAlert_ReturnsTrue_ForCancelledTask()
    {
        var coordinator = new TrayModeCoordinator();
        var task        = new BackgroundTaskInfo { Name = "Task", State = BackgroundTaskState.Cancelled };

        Assert.True(coordinator.ShouldShowTaskCompletionAlert(task));
    }

    [Fact]
    public void ShouldShowTaskCompletionAlert_ReturnsFalse_ForRunningTask()
    {
        var coordinator = new TrayModeCoordinator();
        var task        = new BackgroundTaskInfo { Name = "Scan", State = BackgroundTaskState.Running };

        Assert.False(coordinator.ShouldShowTaskCompletionAlert(task));
    }

    [Fact]
    public void ShouldShowTaskCompletionAlert_ReturnsFalse_ForUnknownState()
    {
        var coordinator = new TrayModeCoordinator();
        var task        = new BackgroundTaskInfo { Name = "Scan", State = (BackgroundTaskState)999 };

        Assert.False(coordinator.ShouldShowTaskCompletionAlert(task));
    }

    // ── BuildTaskAlertMessage ──────────────────────────────────────���──────────

    [Fact]
    public void BuildTaskAlertMessage_UsesTaskMessage_WhenPresent()
    {
        var coordinator = new TrayModeCoordinator();
        var task        = new BackgroundTaskInfo
        {
            Name    = "Download",
            State   = BackgroundTaskState.Completed,
            Message = "Completed",
        };

        var result = coordinator.BuildTaskAlertMessage(task);

        Assert.Contains("Download", result);
        Assert.Contains("Completed", result);
    }

    [Fact]
    public void BuildTaskAlertMessage_FallsBackToState_WhenMessageEmpty()
    {
        var coordinator = new TrayModeCoordinator();
        var task        = new BackgroundTaskInfo
        {
            Name    = "Backup",
            State   = BackgroundTaskState.Completed,
            Message = string.Empty,
        };

        var result = coordinator.BuildTaskAlertMessage(task);

        Assert.Contains("Backup", result);
        Assert.Contains("completed", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTaskAlertMessage_FailedState_ContainsFailedWord()
    {
        var coordinator = new TrayModeCoordinator();
        var task        = new BackgroundTaskInfo
        {
            Name  = "Restore",
            State = BackgroundTaskState.Failed,
        };

        var result = coordinator.BuildTaskAlertMessage(task);

        Assert.Contains("Restore", result);
        Assert.Contains("failed", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTaskAlertMessage_CancelledState_ContainsCancelledWord()
    {
        var coordinator = new TrayModeCoordinator();
        var task        = new BackgroundTaskInfo
        {
            Name  = "Upload",
            State = BackgroundTaskState.Cancelled,
        };

        var result = coordinator.BuildTaskAlertMessage(task);

        Assert.Contains("Upload", result);
        Assert.Contains("cancelled", result, StringComparison.OrdinalIgnoreCase);
    }
}
