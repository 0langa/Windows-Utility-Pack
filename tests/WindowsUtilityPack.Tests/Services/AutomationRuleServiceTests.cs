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

    [Fact]
    public void CreateRuleFromTemplate_ReturnsExpectedRule()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));

            var rule = service.CreateRuleFromTemplate("cpu-sustained");

            Assert.Equal(AutomationTriggerType.HighCpuPercent, rule.TriggerType);
            Assert.Equal(85, rule.Threshold);
            Assert.True(rule.Enabled);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task DryRunAsync_ReportsTriggeredRules()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));
            _ = await service.SaveRuleAsync(new AutomationRule
            {
                Name = "High RAM",
                TriggerType = AutomationTriggerType.HighRamPercent,
                Threshold = 80,
                CooldownMinutes = 15,
                Enabled = true,
                ActionType = AutomationActionType.ShowNotification,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });

            var results = await service.DryRunAsync(new AutomationVitalsSnapshot
            {
                DiskFreeGb = 100,
                CpuPercent = 20,
                RamUsedPercent = 92,
            });

            var result = Assert.Single(results);
            Assert.True(result.Triggered);
            Assert.Contains("would trigger", result.Detail, StringComparison.OrdinalIgnoreCase);
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