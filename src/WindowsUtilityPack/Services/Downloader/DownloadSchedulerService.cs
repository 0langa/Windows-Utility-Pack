using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>One-time queue scheduler for start and pause actions.</summary>
public sealed class DownloadSchedulerService : IDownloadSchedulerService, IDisposable
{
    private readonly object _sync = new();
    private Timer? _startTimer;
    private Timer? _pauseTimer;

    public DateTimeOffset? ScheduledStartAt { get; private set; }

    public DateTimeOffset? ScheduledPauseAt { get; private set; }

    public event EventHandler<DownloaderScheduledAction>? ActionTriggered;

    public void ScheduleStart(DateTimeOffset when)
    {
        lock (_sync)
        {
            _startTimer?.Dispose();

            var delay = when - DateTimeOffset.Now;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            _startTimer = new Timer(_ => Trigger(DownloaderScheduledAction.StartQueue), null, delay, Timeout.InfiniteTimeSpan);
            ScheduledStartAt = when;
        }
    }

    public void SchedulePause(DateTimeOffset when)
    {
        lock (_sync)
        {
            _pauseTimer?.Dispose();

            var delay = when - DateTimeOffset.Now;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            _pauseTimer = new Timer(_ => Trigger(DownloaderScheduledAction.PauseQueue), null, delay, Timeout.InfiniteTimeSpan);
            ScheduledPauseAt = when;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _startTimer?.Dispose();
            _pauseTimer?.Dispose();
            _startTimer = null;
            _pauseTimer = null;
            ScheduledStartAt = null;
            ScheduledPauseAt = null;
        }
    }

    private void Trigger(DownloaderScheduledAction action)
    {
        ActionTriggered?.Invoke(this, action);

        lock (_sync)
        {
            if (action == DownloaderScheduledAction.StartQueue)
            {
                _startTimer?.Dispose();
                _startTimer = null;
                ScheduledStartAt = null;
            }
            else
            {
                _pauseTimer?.Dispose();
                _pauseTimer = null;
                ScheduledPauseAt = null;
            }
        }
    }

    public void Dispose()
    {
        Clear();
    }
}
