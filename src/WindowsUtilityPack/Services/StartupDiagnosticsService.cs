using System.Globalization;
using System.Text;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Produces Startup Manager diagnostics summaries and export payloads.
/// </summary>
public interface IStartupDiagnosticsService
{
    StartupDiagnosticsSummary Summarize(IReadOnlyList<StartupEntryDiagnostic> entries);

    string BuildCsv(IReadOnlyList<StartupEntryDiagnostic> entries);

    string BuildDiagnosticsReport(IReadOnlyList<StartupEntryDiagnostic> entries, bool hklmEntriesSkipped);
}

/// <inheritdoc/>
public sealed class StartupDiagnosticsService : IStartupDiagnosticsService
{
    public StartupDiagnosticsSummary Summarize(IReadOnlyList<StartupEntryDiagnostic> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new StartupDiagnosticsSummary
        {
            TotalEntries = entries.Count,
            EnabledEntries = entries.Count(e => e.IsEnabled),
            DisabledEntries = entries.Count(e => !e.IsEnabled),
            MissingTargetEntries = entries.Count(e => !e.TargetExists),
            RiskFlaggedEntries = entries.Count(e => e.IsPotentiallyRisky),
            MachineScopeEntries = entries.Count(e => string.Equals(e.Source, "HKLM", StringComparison.OrdinalIgnoreCase)),
        };
    }

    public string BuildCsv(IReadOnlyList<StartupEntryDiagnostic> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var sb = new StringBuilder();
        sb.AppendLine("Name,Command,Enabled,Source,ExecutablePath,TargetExists,RiskFlagged");

        foreach (var entry in entries)
        {
            sb.Append(Escape(entry.Name)).Append(',')
              .Append(Escape(entry.Command)).Append(',')
              .Append(entry.IsEnabled ? "true" : "false").Append(',')
              .Append(Escape(entry.Source)).Append(',')
              .Append(Escape(entry.ExecutablePath)).Append(',')
              .Append(entry.TargetExists ? "true" : "false").Append(',')
              .Append(entry.IsPotentiallyRisky ? "true" : "false")
              .AppendLine();
        }

        return sb.ToString();
    }

    public string BuildDiagnosticsReport(IReadOnlyList<StartupEntryDiagnostic> entries, bool hklmEntriesSkipped)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var summary = Summarize(entries);
        var sb = new StringBuilder();

        sb.AppendLine("=== Startup Manager Diagnostics ===");
        sb.AppendLine($"Generated (UTC): {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Total entries: {summary.TotalEntries}");
        sb.AppendLine($"Enabled: {summary.EnabledEntries}");
        sb.AppendLine($"Disabled: {summary.DisabledEntries}");
        sb.AppendLine($"Machine scope (HKLM): {summary.MachineScopeEntries}");
        sb.AppendLine($"Missing executable target: {summary.MissingTargetEntries}");
        sb.AppendLine($"Risk flagged commands: {summary.RiskFlaggedEntries}");
        sb.AppendLine($"HKLM read skipped: {(hklmEntriesSkipped ? "yes" : "no")}");
        sb.AppendLine();
        sb.AppendLine("Risk-flagged entries:");

        var risky = entries.Where(e => e.IsPotentiallyRisky).Take(25).ToList();
        if (risky.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var entry in risky)
            {
                sb.Append("  - ").Append(entry.Name)
                  .Append(" [").Append(entry.Source).Append("] ")
                  .AppendLine(entry.Command);
            }
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}