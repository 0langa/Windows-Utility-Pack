using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services.Storage;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

public sealed class CleanupAutomationPolicyServiceTests
{
    [Fact]
    public void BuildPlan_Conservative_SelectsOnlyLowRiskTempAndEmpty()
    {
        var service = new CleanupAutomationPolicyService();
        var input = BuildRecommendations();

        var plan = service.BuildPlan(input, new CleanupAutomationPolicyOptions
        {
            Mode = CleanupAutomationPolicyMode.Conservative,
        });

        Assert.All(plan.Selected, rec =>
        {
            Assert.True(rec.Category is CleanupCategory.TemporaryFiles or CleanupCategory.EmptyFolders);
            Assert.Equal(CleanupRisk.Low, rec.Risk);
        });
    }

    [Fact]
    public void BuildPlan_Balanced_CanExcludeDuplicates()
    {
        var service = new CleanupAutomationPolicyService();
        var input = BuildRecommendations();

        var plan = service.BuildPlan(input, new CleanupAutomationPolicyOptions
        {
            Mode = CleanupAutomationPolicyMode.Balanced,
            IncludeDuplicateRecommendations = false,
            IncludeMediumRisk = true,
        });

        Assert.DoesNotContain(plan.Selected, r => r.Category == CleanupCategory.DuplicateFiles);
    }

    [Fact]
    public void BuildPlan_Aggressive_WithRiskFlagsAndMinimumSavings_FiltersCorrectly()
    {
        var service = new CleanupAutomationPolicyService();
        var input = BuildRecommendations();

        var plan = service.BuildPlan(input, new CleanupAutomationPolicyOptions
        {
            Mode = CleanupAutomationPolicyMode.Aggressive,
            IncludeMediumRisk = true,
            IncludeHighRisk = false,
            MinimumSavingsBytes = 10 * 1024 * 1024,
        });

        Assert.All(plan.Selected, rec =>
        {
            Assert.True(rec.PotentialSavingsBytes >= 10 * 1024 * 1024);
            Assert.NotEqual(CleanupRisk.High, rec.Risk);
        });
    }

    private static IReadOnlyList<CleanupRecommendation> BuildRecommendations()
    {
        return
        [
            NewRec(CleanupCategory.TemporaryFiles, CleanupRisk.Low, 12 * 1024 * 1024, @"C:\temp\a.tmp"),
            NewRec(CleanupCategory.EmptyFolders, CleanupRisk.Low, 0, @"C:\temp\empty"),
            NewRec(CleanupCategory.DuplicateFiles, CleanupRisk.Low, 25 * 1024 * 1024, @"C:\data\dup.bin"),
            NewRec(CleanupCategory.CacheLikeFiles, CleanupRisk.Medium, 8 * 1024 * 1024, @"C:\data\cache.cache"),
            NewRec(CleanupCategory.LargeStaleFiles, CleanupRisk.High, 100 * 1024 * 1024, @"C:\data\old.iso"),
        ];
    }

    private static CleanupRecommendation NewRec(CleanupCategory category, CleanupRisk risk, long size, string path)
    {
        return new CleanupRecommendation
        {
            Category = category,
            Risk = risk,
            Rationale = "Test",
            Item = new StorageItem
            {
                FullPath = path,
                Name = System.IO.Path.GetFileName(path),
                SizeBytes = size,
                TotalSizeBytes = size,
            },
        };
    }
}