using WindowsUtilityPack.Services.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class DownloadSchedulerServiceTests
{
    [Fact]
    public void ScheduleStart_SetsTimestamp()
    {
        using var scheduler = new DownloadSchedulerService();
        var when = DateTimeOffset.Now.AddMinutes(5);

        scheduler.ScheduleStart(when);

        Assert.True(scheduler.ScheduledStartAt.HasValue);
        Assert.Equal(when, scheduler.ScheduledStartAt.Value);
    }

    [Fact]
    public void Clear_ResetsPlannedActions()
    {
        using var scheduler = new DownloadSchedulerService();
        scheduler.ScheduleStart(DateTimeOffset.Now.AddMinutes(2));
        scheduler.SchedulePause(DateTimeOffset.Now.AddMinutes(3));

        scheduler.Clear();

        Assert.Null(scheduler.ScheduledStartAt);
        Assert.Null(scheduler.ScheduledPauseAt);
    }
}
