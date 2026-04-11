using System.IO;
using System.Reflection;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class AutomationRuleServiceTests
{
    [Fact]
    public async Task SaveRuleAsync_ThenGetRulesAsync_RoundTripsRule()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));

            _ = await service.SaveRuleAsync(new AutomationRule
            {
                Name = "Low disk",
                TriggerType = AutomationTriggerType.LowDiskFreeGb,
                Threshold = 10,
                CooldownMinutes = 10,
                Enabled = true,
                ActionType = AutomationActionType.ShowNotification,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });

            var rules = await service.GetRulesAsync();
            Assert.Single(rules);
            Assert.Equal("Low disk", rules[0].Name);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task EvaluateAsync_RespectsCooldown()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));
            _ = await service.SaveRuleAsync(new AutomationRule
            {
                Name = "High CPU",
                TriggerType = AutomationTriggerType.HighCpuPercent,
                Threshold = 10,
                CooldownMinutes = 60,
                Enabled = true,
                ActionType = AutomationActionType.ShowNotification,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });

            var vitals = new SystemVitalsService();
            SetPrivateProperty(vitals, "CpuPercent", 90f);

            var first = await service.EvaluateAsync(vitals);
            var second = await service.EvaluateAsync(vitals);

            Assert.Single(first);
            Assert.Empty(second);

            vitals.Dispose();
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static void SetPrivateProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        property!.SetValue(target, value);
    }

    private static string GetTempDatabasePath()
        => Path.Combine(Path.GetTempPath(), $"wup-tests-{Guid.NewGuid():N}.db");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}