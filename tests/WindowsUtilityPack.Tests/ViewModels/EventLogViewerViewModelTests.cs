using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.SystemUtilities.EventLogViewer;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class EventLogViewerViewModelTests
{
    [Fact]
    public async Task Refresh_LoadsEntriesFromService()
    {
        var vm = new EventLogViewerViewModel(new StubService(), new StubClipboard());

        await vm.RefreshAsync();

        Assert.NotEmpty(vm.Entries);
        Assert.Equal("Loaded 1 events from Application.", vm.StatusMessage);
    }

    private sealed class StubService : IWindowsEventLogService
    {
        public Task<IReadOnlyList<WindowsEventLogRecord>> QueryAsync(
            string logName,
            string? sourceFilter,
            string? levelFilter,
            int? eventIdFilter,
            DateTime? sinceUtc,
            int limit,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WindowsEventLogRecord> rows =
            [
                new WindowsEventLogRecord
                {
                    TimestampUtc = DateTime.UtcNow,
                    LogName = logName,
                    Source = "TestSource",
                    EventId = 1001,
                    Level = "Error",
                    Message = "Sample event",
                },
            ];

            return Task.FromResult(rows);
        }
    }

    private sealed class StubClipboard : IClipboardService
    {
        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }

        public void SetText(string text) { }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
}