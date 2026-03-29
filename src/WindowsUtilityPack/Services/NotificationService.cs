namespace WindowsUtilityPack.Services;

/// <summary>
/// Default implementation of <see cref="INotificationService"/>.
/// Raises <see cref="NotificationRequested"/> for subscribers to display.
/// Currently the event is wired but the UI notification panel is a future addition;
/// the service is in place so ViewModels can call it now without future refactoring.
/// </summary>
public class NotificationService : INotificationService
{
    /// <inheritdoc/>
    public event EventHandler<NotificationEventArgs>? NotificationRequested;

    /// <inheritdoc/>
    public void ShowInfo(string message) => Raise(message, NotificationType.Info);

    /// <inheritdoc/>
    public void ShowSuccess(string message) => Raise(message, NotificationType.Success);

    /// <inheritdoc/>
    public void ShowError(string message) => Raise(message, NotificationType.Error);

    private void Raise(string message, NotificationType type)
        => NotificationRequested?.Invoke(this, new NotificationEventArgs(message, type));
}
