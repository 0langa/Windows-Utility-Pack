namespace WindowsUtilityPack.Services;

public interface INotificationService
{
    void ShowInfo(string message);
    void ShowSuccess(string message);
    void ShowError(string message);
    event EventHandler<NotificationEventArgs>? NotificationRequested;
}

public class NotificationEventArgs : EventArgs
{
    public string Message { get; }
    public NotificationType Type { get; }
    public NotificationEventArgs(string message, NotificationType type) { Message = message; Type = type; }
}

public enum NotificationType { Info, Success, Error }
