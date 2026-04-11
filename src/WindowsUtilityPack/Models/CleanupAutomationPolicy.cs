namespace WindowsUtilityPack.Models;

/// <summary>
/// Preset cleanup policy modes.
/// </summary>
public enum CleanupAutomationPolicyMode
{
    Conservative,
    Balanced,
    Aggressive,
}

/// <summary>
/// Policy options used to build cleanup selection plans.
/// </summary>
public sealed class CleanupAutomationPolicyOptions
{
    public CleanupAutomationPolicyMode Mode { get; init; } = CleanupAutomationPolicyMode.Balanced;

    public bool IncludeMediumRisk { get; init; }

    public bool IncludeHighRisk { get; init; }

    public bool IncludeDuplicateRecommendations { get; init; } = true;

    public long MinimumSavingsBytes { get; init; }
}

/// <summary>
/// Result of policy evaluation against cleanup recommendations.
/// </summary>
public sealed class CleanupAutomationPolicyPlan
{
    public required IReadOnlyList<CleanupRecommendation> Selected { get; init; }

    public required int TotalRecommendations { get; init; }

    public required int SelectedCount { get; init; }

    public required long EstimatedSavingsBytes { get; init; }

    public string EstimatedSavingsFormatted => StorageItem.FormatBytes(EstimatedSavingsBytes);
}