namespace WindowsUtilityPack.Models;

/// <summary>
/// Supported built-in automation triggers.
/// </summary>
public enum AutomationTriggerType
{
    LowDiskFreeGb,
    HighCpuPercent,
    HighRamPercent,
}

/// <summary>
/// Supported built-in automation actions.
/// </summary>
public enum AutomationActionType
{
    ShowNotification,
}

/// <summary>
/// Persisted automation rule definition.
/// </summary>
public sealed class AutomationRule
{
    public long Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public AutomationTriggerType TriggerType { get; init; }

    public double Threshold { get; init; }

    public int CooldownMinutes { get; init; }

    public bool Enabled { get; init; }

    public AutomationActionType ActionType { get; init; }

    public DateTime CreatedUtc { get; init; }

    public DateTime UpdatedUtc { get; init; }
}

/// <summary>
/// Runtime outcome when a rule triggers.
/// </summary>
public sealed class AutomationRuleAlert
{
    public required AutomationRule Rule { get; init; }

    public required string Message { get; init; }

    public DateTime TriggeredUtc { get; init; }
}

/// <summary>
/// Template definition for quickly creating common automation rules.
/// </summary>
public sealed class AutomationRuleTemplate
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required AutomationTriggerType TriggerType { get; init; }

    public required double Threshold { get; init; }

    public required int CooldownMinutes { get; init; }
}

/// <summary>
/// Snapshot values used for dry-run simulation of automation rules.
/// </summary>
public sealed class AutomationVitalsSnapshot
{
    public double DiskFreeGb { get; init; }

    public float CpuPercent { get; init; }

    public float RamUsedPercent { get; init; }
}

/// <summary>
/// Dry-run outcome for a single automation rule.
/// </summary>
public sealed class AutomationRuleSimulationResult
{
    public required string RuleName { get; init; }

    public required bool Triggered { get; init; }

    public required string Detail { get; init; }
}