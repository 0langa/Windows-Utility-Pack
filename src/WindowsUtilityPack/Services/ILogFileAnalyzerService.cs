using System.IO;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Parses plain-text log files and produces filtered summaries.
/// </summary>
public interface ILogFileAnalyzerService
{
    Task<LogAnalysisResult> AnalyzeAsync(
        string filePath,
        string? textFilter,
        LogSeverity? minSeverity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default line-oriented log analyzer.
/// </summary>
public sealed class LogFileAnalyzerService : ILogFileAnalyzerService
{
    public Task<LogAnalysisResult> AnalyzeAsync(string filePath, string? textFilter, LogSeverity? minSeverity, CancellationToken cancellationToken = default)
        => Task.Run(() => AnalyzeCoreAsync(filePath, textFilter, minSeverity, cancellationToken), cancellationToken);

    private static async Task<LogAnalysisResult> AnalyzeCoreAsync(
        string filePath,
        string? textFilter,
        LogSeverity? minSeverity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A log file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Log file was not found.", filePath);
        }

        var filter = textFilter?.Trim();
        var entries = new List<LogEntryRow>();
        var total = 0;
        var matched = 0;
        var errorCount = 0;
        var warnCount = 0;
        var infoCount = 0;

        using var stream = File.OpenRead(filePath);
        if (stream.Length > 256L * 1024 * 1024) throw new InvalidDataException("Log analysis is limited to 256 MiB files.");
        using var reader = new StreamReader(stream);
        var lineNo = 0;

        await foreach (var line in BoundedTextFile.ReadLinesAsync(reader, 65_536, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNo++;
            total++;

            var severity = InferSeverity(line);
            if (severity == LogSeverity.Error || severity == LogSeverity.Fatal)
            {
                errorCount++;
            }
            else if (severity == LogSeverity.Warn)
            {
                warnCount++;
            }
            else if (severity == LogSeverity.Info)
            {
                infoCount++;
            }

            if (!string.IsNullOrWhiteSpace(filter) &&
                !line.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (minSeverity is not null && severity < minSeverity.Value)
            {
                continue;
            }

            if (entries.Count < 5_000) entries.Add(new LogEntryRow
            {
                LineNumber = lineNo,
                Severity = severity,
                Message = line.Length <= 4096 ? line : line[..4096] + "… [line truncated]",
            });
            matched++;
        }

        return new LogAnalysisResult
        {
            SourcePath = filePath,
            TotalLines = total,
            MatchedLines = matched,
            ErrorCount = errorCount,
            WarnCount = warnCount,
            InfoCount = infoCount,
            Entries = entries,
        };
    }

    private static LogSeverity InferSeverity(string line)
    {
        if (line.Contains("FATAL", StringComparison.OrdinalIgnoreCase)) return LogSeverity.Fatal;
        if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)) return LogSeverity.Error;
        if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase)) return LogSeverity.Warn;
        if (line.Contains("INFO", StringComparison.OrdinalIgnoreCase)) return LogSeverity.Info;
        if (line.Contains("DEBUG", StringComparison.OrdinalIgnoreCase)) return LogSeverity.Debug;
        if (line.Contains("TRACE", StringComparison.OrdinalIgnoreCase)) return LogSeverity.Trace;
        return LogSeverity.Unknown;
    }
}
