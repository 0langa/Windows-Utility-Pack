using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Coordinates tray-mode behavior decisions used by the shell window.
/// </summary>
public interface ITrayModeCoordinator
{
    bool ShouldHideOnMinimize(AppSettings settings);

    bool ShouldInterceptClose(AppSettings settings, bool explicitExitRequested);

    bool ShouldShowTrayAlert(AppSettings settings, bool isWindowHidden);

    bool ShouldShowTaskCompletionAlert(BackgroundTaskInfo taskInfo);

    string BuildTaskAlertMessage(BackgroundTaskInfo taskInfo);
}

/// <summary>
/// Default tray-mode decision coordinator.
/// </summary>
public sealed class TrayModeCoordinator : ITrayModeCoordinator
{
    public bool ShouldHideOnMinimize(AppSettings settings)
        => settings.TrayModeEnabled && settings.MinimizeToTray;

    public bool ShouldInterceptClose(AppSettings settings, bool explicitExitRequested)
        => settings.TrayModeEnabled && !explicitExitRequested;

    public bool ShouldShowTrayAlert(AppSettings settings, bool isWindowHidden)
        => settings.TrayModeEnabled && settings.TrayAlertsEnabled && isWindowHidden;

    public bool ShouldShowTaskCompletionAlert(BackgroundTaskInfo taskInfo)
        => taskInfo.State is BackgroundTaskState.Completed or BackgroundTaskState.Failed or BackgroundTaskState.Cancelled;

    public string BuildTaskAlertMessage(BackgroundTaskInfo taskInfo)
    {
        var state = taskInfo.State switch
        {
            BackgroundTaskState.Completed => "completed",
            BackgroundTaskState.Failed => "failed",
            BackgroundTaskState.Cancelled => "cancelled",
            _ => "updated",
        };

        return string.IsNullOrWhiteSpace(taskInfo.Message)
            ? $"{taskInfo.Name} {state}."
            : $"{taskInfo.Name}: {taskInfo.Message}";
    }
}