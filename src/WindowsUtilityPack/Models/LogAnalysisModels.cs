namespace WindowsUtilityPack.Models;

/// <summary>
/// Normalized severity level inferred from raw log text.
/// </summary>
public enum LogSeverity
{
    Unknown,
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Fatal,
}

/// <summary>
/// Parsed log entry row.
/// </summary>
public sealed class LogEntryRow
{
    public int LineNumber { get; init; }

    public LogSeverity Severity { get; init; }

    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Summary and rows produced by log analysis.
/// </summary>
public sealed class LogAnalysisResult
{
    public string SourcePath { get; init; } = string.Empty;

    public int TotalLines { get; init; }

    public int MatchedLines { get; init; }

    public int ErrorCount { get; init; }

    public int WarnCount { get; init; }

    public int InfoCount { get; init; }

    public IReadOnlyList<LogEntryRow> Entries { get; init; } = [];
}