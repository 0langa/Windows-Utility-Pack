namespace WindowsUtilityPack.Models;

/// <summary>
/// Diagnostic projection for a startup entry.
/// </summary>
public sealed class StartupEntryDiagnostic
{
    public string Name { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public string Source { get; init; } = string.Empty;

    public string ExecutablePath { get; init; } = string.Empty;

    public bool TargetExists { get; init; }

    public bool IsPotentiallyRisky { get; init; }
}

/// <summary>
/// Summary counts for startup diagnostics.
/// </summary>
public sealed class StartupDiagnosticsSummary
{
    public int TotalEntries { get; init; }

    public int EnabledEntries { get; init; }

    public int DisabledEntries { get; init; }

    public int MissingTargetEntries { get; init; }

    public int RiskFlaggedEntries { get; init; }

    public int MachineScopeEntries { get; init; }
}