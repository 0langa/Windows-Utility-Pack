using System.IO;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class LogFileAnalyzerServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ComputesSeverityCounts()
    {
        var path = CreateTempLog(
        [
            "2026-01-01 INFO Startup",
            "2026-01-01 WARN Slow API",
            "2026-01-01 ERROR Crash",
        ]);

        try
        {
            var service = new LogFileAnalyzerService();
            var result = await service.AnalyzeAsync(path, null, null);

            Assert.Equal(3, result.TotalLines);
            Assert.Equal(1, result.ErrorCount);
            Assert.Equal(1, result.WarnCount);
            Assert.Equal(1, result.InfoCount);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_AppliesTextAndSeverityFilter()
    {
        var path = CreateTempLog(
        [
            "INFO Cache warmup",
            "WARN Queue lag",
            "ERROR Queue timeout",
            "DEBUG Queue internals",
        ]);

        try
        {
            var service = new LogFileAnalyzerService();
            var result = await service.AnalyzeAsync(path, "queue", LogSeverity.Warn);

            Assert.Equal(2, result.MatchedLines);
            Assert.DoesNotContain(result.Entries, e => e.Severity == LogSeverity.Info);
            Assert.DoesNotContain(result.Entries, e => e.Severity == LogSeverity.Debug);
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static string CreateTempLog(IReadOnlyList<string> lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wup-log-{Guid.NewGuid():N}.log");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch { }
    }
}