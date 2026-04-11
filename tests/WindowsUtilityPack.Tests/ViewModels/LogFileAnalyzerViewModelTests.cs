using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.DeveloperProductivity.LogFileAnalyzer;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class LogFileAnalyzerViewModelTests
{
    [Fact]
    public async Task AnalyzeAsync_LoadsEntriesAndCounters()
    {
        var vm = new LogFileAnalyzerViewModel(new StubAnalyzer(), new StubClipboard(), new StubDialogs())
        {
            FilePath = "sample.log",
        };

        await vm.AnalyzeAsync();

        Assert.Equal(10, vm.TotalLines);
        Assert.Equal(2, vm.Entries.Count);
        Assert.Equal(1, vm.ErrorCount);
    }

    private sealed class StubAnalyzer : ILogFileAnalyzerService
    {
        public Task<LogAnalysisResult> AnalyzeAsync(string filePath, string? textFilter, LogSeverity? minSeverity, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LogAnalysisResult
            {
                SourcePath = filePath,
                TotalLines = 10,
                MatchedLines = 2,
                ErrorCount = 1,
                WarnCount = 1,
                InfoCount = 5,
                Entries =
                [
                    new LogEntryRow { LineNumber = 1, Severity = LogSeverity.Warn, Message = "warn" },
                    new LogEntryRow { LineNumber = 2, Severity = LogSeverity.Error, Message = "error" },
                ],
            });
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

    private sealed class StubDialogs : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;

        public void ShowError(string title, string message) { }

        public void ShowInfo(string title, string message) { }
    }
}