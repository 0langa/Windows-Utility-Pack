using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Builds cleanup selection plans from policy options.
/// </summary>
public interface ICleanupAutomationPolicyService
{
    CleanupAutomationPolicyPlan BuildPlan(
        IReadOnlyList<CleanupRecommendation> recommendations,
        CleanupAutomationPolicyOptions options);
}

/// <summary>
/// Default cleanup policy planner.
/// </summary>
public sealed class CleanupAutomationPolicyService : ICleanupAutomationPolicyService
{
    public CleanupAutomationPolicyPlan BuildPlan(
        IReadOnlyList<CleanupRecommendation> recommendations,
        CleanupAutomationPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(recommendations);
        ArgumentNullException.ThrowIfNull(options);

        var selected = recommendations
            .Where(r => IsCategoryAllowed(r, options.Mode, options.IncludeDuplicateRecommendations))
            .Where(r => IsRiskAllowed(r.Risk, options.IncludeMediumRisk, options.IncludeHighRisk))
            .Where(r => r.PotentialSavingsBytes >= Math.Max(0, options.MinimumSavingsBytes))
            .OrderByDescending(r => r.PotentialSavingsBytes)
            .GroupBy(r => r.Item.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return new CleanupAutomationPolicyPlan
        {
            Selected = selected,
            TotalRecommendations = recommendations.Count,
            SelectedCount = selected.Count,
            EstimatedSavingsBytes = selected.Sum(r => r.PotentialSavingsBytes),
        };
    }

    private static bool IsRiskAllowed(CleanupRisk risk, bool includeMediumRisk, bool includeHighRisk)
        => risk switch
        {
            CleanupRisk.Low => true,
            CleanupRisk.Medium => includeMediumRisk,
            CleanupRisk.High => includeHighRisk,
            _ => false,
        };

    private static bool IsCategoryAllowed(
        CleanupRecommendation recommendation,
        CleanupAutomationPolicyMode mode,
        bool includeDuplicateRecommendations)
    {
        if (!includeDuplicateRecommendations && recommendation.Category == CleanupCategory.DuplicateFiles)
        {
            return false;
        }

        return mode switch
        {
            CleanupAutomationPolicyMode.Conservative => recommendation.Category is CleanupCategory.TemporaryFiles or CleanupCategory.EmptyFolders,
            CleanupAutomationPolicyMode.Balanced => recommendation.Category is CleanupCategory.TemporaryFiles
                or CleanupCategory.EmptyFolders
                or CleanupCategory.CacheLikeFiles
                or CleanupCategory.DuplicateFiles,
            CleanupAutomationPolicyMode.Aggressive => true,
            _ => false,
        };
    }
}