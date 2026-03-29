namespace WindowsUtilityPack.Services;

public class NotificationService : INotificationService
{
    public event EventHandler<NotificationEventArgs>? NotificationRequested;

    public void ShowInfo(string message) => Raise(message, NotificationType.Info);
    public void ShowSuccess(string message) => Raise(message, NotificationType.Success);
    public void ShowError(string message) => Raise(message, NotificationType.Error);

    private void Raise(string message, NotificationType type)
        => NotificationRequested?.Invoke(this, new NotificationEventArgs(message, type));
}
