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