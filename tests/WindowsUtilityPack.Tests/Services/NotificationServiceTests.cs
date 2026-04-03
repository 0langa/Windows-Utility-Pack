using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Unit tests for <see cref="NotificationService"/>.
/// Verifies that notification methods raise the <see cref="INotificationService.NotificationRequested"/> event.
/// </summary>
public class NotificationServiceTests
{
    [Fact]
    public void ShowInfo_RaisesNotificationRequested()
    {
        var svc = new NotificationService();
        NotificationEventArgs? received = null;
        svc.NotificationRequested += (_, e) => received = e;

        svc.ShowInfo("Test info");

        Assert.NotNull(received);
        Assert.Equal("Test info", received!.Message);
        Assert.Equal(NotificationType.Info, received.Type);
    }

    [Fact]
    public void ShowSuccess_RaisesNotificationRequested()
    {
        var svc = new NotificationService();
        NotificationEventArgs? received = null;
        svc.NotificationRequested += (_, e) => received = e;

        svc.ShowSuccess("Done!");

        Assert.NotNull(received);
        Assert.Equal("Done!", received!.Message);
        Assert.Equal(NotificationType.Success, received.Type);
    }

    [Fact]
    public void ShowError_RaisesNotificationRequested()
    {
        var svc = new NotificationService();
        NotificationEventArgs? received = null;
        svc.NotificationRequested += (_, e) => received = e;

        svc.ShowError("Something failed");

        Assert.NotNull(received);
        Assert.Equal("Something failed", received!.Message);
        Assert.Equal(NotificationType.Error, received.Type);
    }

    [Fact]
    public void NoSubscriber_DoesNotThrow()
    {
        var svc = new NotificationService();
        var ex = Record.Exception(() => svc.ShowInfo("No listeners"));
        Assert.Null(ex);
    }
}
