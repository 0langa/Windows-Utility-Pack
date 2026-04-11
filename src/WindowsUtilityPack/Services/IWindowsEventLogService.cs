using System.Diagnostics;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Represents a normalized Windows event log record.
/// </summary>
public sealed class WindowsEventLogRecord
{
    public DateTime TimestampUtc { get; init; }

    public string LogName { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public int EventId { get; init; }

    public string Level { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Provides filtered reads from Windows event logs.
/// </summary>
public interface IWindowsEventLogService
{
    Task<IReadOnlyList<WindowsEventLogRecord>> QueryAsync(
        string logName,
        string? sourceFilter,
        string? levelFilter,
        int? eventIdFilter,
        DateTime? sinceUtc,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads event log records using <see cref="EventLog"/>.
/// </summary>
public sealed class WindowsEventLogService : IWindowsEventLogService
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
        if (string.IsNullOrWhiteSpace(logName))
        {
            return Task.FromResult<IReadOnlyList<WindowsEventLogRecord>>([]);
        }

        limit = Math.Clamp(limit, 1, 2000);
        var source = sourceFilter?.Trim();
        var level = levelFilter?.Trim();
        var since = sinceUtc ?? DateTime.UtcNow.AddDays(-1);

        try
        {
            using var eventLog = new EventLog(logName);
            var records = new List<WindowsEventLogRecord>(limit);

            for (var i = eventLog.Entries.Count - 1; i >= 0 && records.Count < limit; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = eventLog.Entries[i];
                var writtenUtc = entry.TimeGenerated.Kind == DateTimeKind.Utc
                    ? entry.TimeGenerated
                    : entry.TimeGenerated.ToUniversalTime();
                if (writtenUtc < since)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(source) &&
                    !entry.Source.Contains(source, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (eventIdFilter.HasValue && entry.InstanceId != (uint)eventIdFilter.Value)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(level) &&
                    !entry.EntryType.ToString().Contains(level, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                records.Add(new WindowsEventLogRecord
                {
                    TimestampUtc = writtenUtc,
                    LogName = logName,
                    Source = entry.Source,
                    EventId = (int)entry.InstanceId,
                    Level = entry.EntryType.ToString(),
                    Message = entry.Message,
                });
            }

            return Task.FromResult<IReadOnlyList<WindowsEventLogRecord>>(records);
        }
        catch
        {
            return Task.FromResult<IReadOnlyList<WindowsEventLogRecord>>([]);
        }
    }
}