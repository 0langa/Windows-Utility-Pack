namespace WindowsUtilityPack.Services;

/// <summary>
/// Contract for raising in-app toast/snackbar-style notifications.
/// Subscribers (typically the main window) listen to <see cref="NotificationRequested"/>
/// and display the message in an appropriate UI control.
/// </summary>
public interface INotificationService
{
    /// <summary>Shows an informational notification.</summary>
    void ShowInfo(string message);

    /// <summary>Shows a success confirmation notification.</summary>
    void ShowSuccess(string message);

    /// <summary>Shows an error notification.</summary>
    void ShowError(string message);

    /// <summary>Raised whenever a notification should be displayed.</summary>
    event EventHandler<NotificationEventArgs>? NotificationRequested;
}

/// <summary>Carries the message text and severity for a notification.</summary>
public class NotificationEventArgs : EventArgs
{
    public string Message { get; }
    public NotificationType Type { get; }
    public NotificationEventArgs(string message, NotificationType type) { Message = message; Type = type; }
}

/// <summary>Severity level of a notification.</summary>
public enum NotificationType { Info, Success, Error }
