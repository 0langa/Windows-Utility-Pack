using System.IO;
using System.Reflection;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class AutomationRuleServiceTests
{
    [Fact]
    public async Task SaveRuleAsync_NewRulesHaveDistinctPersistentIds()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));
            var first = await service.SaveRuleAsync(new AutomationRule { Name = "First", Enabled = true });
            var second = await service.SaveRuleAsync(new AutomationRule { Name = "Second", Enabled = true });
            Assert.True(first.Id > 0);
            Assert.NotEqual(first.Id, second.Id);
            Assert.Equal(2, (await service.GetRulesAsync()).Count);
            Assert.True(await service.DeleteRuleAsync(first.Id));
            Assert.Equal("Second", Assert.Single(await service.GetRulesAsync()).Name);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task DryRunAsync_DisabledRuleDoesNotTriggerOrConsumeCooldown()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));
            await service.SaveRuleAsync(new AutomationRule
            {
                Name = "Disabled", Enabled = false, TriggerType = AutomationTriggerType.HighCpuPercent, Threshold = 10,
            });
            var result = Assert.Single(await service.DryRunAsync(new AutomationVitalsSnapshot { CpuPercent = 90 }));
            Assert.False(result.Triggered);
            Assert.Contains("disabled", result.Detail, StringComparison.OrdinalIgnoreCase);
        }
        finally { TryDelete(path); }
    }

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
                ActionTarget = string.Empty,
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
    public async Task EvaluateAsync_ConcurrentCallsClaimCooldownOnlyOnce()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));
            await service.SaveRuleAsync(new AutomationRule
            {
                Name = "Concurrent", Enabled = true, Threshold = 10, CooldownMinutes = 60,
                TriggerType = AutomationTriggerType.HighCpuPercent,
            });
            using var vitals = new SystemVitalsService();
            SetPrivateProperty(vitals, "CpuPercent", 90f);
            var calls = Enumerable.Range(0, 16).Select(_ => Task.Run(() => service.EvaluateAsync(vitals)));
            var results = await Task.WhenAll(calls);
            Assert.Single(results.SelectMany(result => result));
        }
        finally { TryDelete(path); }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public async Task SaveRuleAsync_RejectsInvalidThresholdBeforePersisting(double threshold)
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SaveRuleAsync(new AutomationRule
            {
                Name = "Invalid", Enabled = true, TriggerType = AutomationTriggerType.HighCpuPercent, Threshold = threshold,
            }));
            Assert.Empty(await service.GetRulesAsync());
        }
        finally { TryDelete(path); }
    }

    [Theory]
    [InlineData(AutomationTriggerType.HighDiskUsagePercent, 80)]
    [InlineData(AutomationTriggerType.ProcessCountExceedsLimit, 50)]
    public async Task DryRunAsync_SupportsEveryAdvertisedTrigger(AutomationTriggerType trigger, double threshold)
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));
            await service.SaveRuleAsync(new AutomationRule { Name = "Trigger", Enabled = true, TriggerType = trigger, Threshold = threshold });
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<AutomationVitalsSnapshot>(
                "{\"DiskUsagePercent\":90,\"ProcessCount\":100}")!;
            Assert.True(Assert.Single(await service.DryRunAsync(snapshot)).Triggered);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task ExistingZeroIdRule_IsMigratedAndCanBeDeleted()
    {
        var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new AutomationRuleService(store);
            await service.SaveRuleAsync(new AutomationRule { Name = "Legacy" });
            using (var connection = store.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE automation_rules SET id=0; PRAGMA user_version=5;";
                command.ExecuteNonQuery();
            }
            var migrated = new AutomationRuleService(new AppDataStoreService(path));
            var saved = Assert.Single(await migrated.GetRulesAsync());
            Assert.True(saved.Id > 0);
            Assert.True(await migrated.DeleteRuleAsync(saved.Id));
        }
        finally { TryDelete(path); }
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
                ActionTarget = string.Empty,
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
                ActionTarget = string.Empty,
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

    [Fact]
    public async Task SaveRuleAsync_LaunchToolWithoutTarget_BackfillsFromNameForCompatibility()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AutomationRuleService(new AppDataStoreService(path));
            var saved = await service.SaveRuleAsync(new AutomationRule
            {
                Name = "clipboard-manager",
                TriggerType = AutomationTriggerType.HighCpuPercent,
                Threshold = 90,
                CooldownMinutes = 5,
                Enabled = true,
                ActionType = AutomationActionType.LaunchTool,
                ActionTarget = string.Empty,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });

            Assert.Equal("clipboard-manager", saved.ActionTarget);
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
