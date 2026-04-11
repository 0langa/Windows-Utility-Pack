using System.Collections.Concurrent;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Persists and evaluates automation rules.
/// </summary>
public interface IAutomationRuleService
{
    Task<IReadOnlyList<AutomationRule>> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<AutomationRule> SaveRuleAsync(AutomationRule rule, CancellationToken cancellationToken = default);

    Task<bool> DeleteRuleAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationRuleAlert>> EvaluateAsync(SystemVitalsService vitals, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default automation rule engine.
/// </summary>
public sealed class AutomationRuleService : IAutomationRuleService
{
    private readonly IAppDataStoreService _store;
    private readonly ConcurrentDictionary<long, DateTime> _lastTriggeredUtc = new();

    public AutomationRuleService(IAppDataStoreService store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _store.EnsureInitialized();
    }

    public async Task<IReadOnlyList<AutomationRule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, name, trigger_type, threshold, cooldown_minutes, enabled, action_type, created_utc, updated_utc
FROM automation_rules
ORDER BY updated_utc DESC, id DESC;";

        var result = new List<AutomationRule>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new AutomationRule
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                TriggerType = Enum.TryParse<AutomationTriggerType>(reader.GetString(2), out var trigger) ? trigger : AutomationTriggerType.LowDiskFreeGb,
                Threshold = reader.GetDouble(3),
                CooldownMinutes = reader.GetInt32(4),
                Enabled = reader.GetInt64(5) == 1,
                ActionType = Enum.TryParse<AutomationActionType>(reader.GetString(6), out var action) ? action : AutomationActionType.ShowNotification,
                CreatedUtc = ParseDate(reader.GetString(7)),
                UpdatedUtc = ParseDate(reader.GetString(8)),
            });
        }

        return result;
    }

    public async Task<AutomationRule> SaveRuleAsync(AutomationRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new ArgumentException("Rule name is required.", nameof(rule));
        }

        var now = DateTime.UtcNow;
        var created = rule.CreatedUtc == default ? now : rule.CreatedUtc;

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO automation_rules(id, name, trigger_type, threshold, cooldown_minutes, enabled, action_type, created_utc, updated_utc)
VALUES($id, $name, $trigger, $threshold, $cooldown, $enabled, $action, $created, $updated)
ON CONFLICT(id) DO UPDATE SET
    name = excluded.name,
    trigger_type = excluded.trigger_type,
    threshold = excluded.threshold,
    cooldown_minutes = excluded.cooldown_minutes,
    enabled = excluded.enabled,
    action_type = excluded.action_type,
    updated_utc = excluded.updated_utc;
SELECT CASE WHEN $id = 0 THEN last_insert_rowid() ELSE $id END;";

        command.Parameters.AddWithValue("$id", rule.Id <= 0 ? 0 : rule.Id);
        command.Parameters.AddWithValue("$name", rule.Name.Trim());
        command.Parameters.AddWithValue("$trigger", rule.TriggerType.ToString());
        command.Parameters.AddWithValue("$threshold", rule.Threshold);
        command.Parameters.AddWithValue("$cooldown", Math.Max(0, rule.CooldownMinutes));
        command.Parameters.AddWithValue("$enabled", rule.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$action", rule.ActionType.ToString());
        command.Parameters.AddWithValue("$created", created.ToString("O"));
        command.Parameters.AddWithValue("$updated", now.ToString("O"));

        var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

        return new AutomationRule
        {
            Id = id,
            Name = rule.Name.Trim(),
            TriggerType = rule.TriggerType,
            Threshold = rule.Threshold,
            CooldownMinutes = Math.Max(0, rule.CooldownMinutes),
            Enabled = rule.Enabled,
            ActionType = rule.ActionType,
            CreatedUtc = created,
            UpdatedUtc = now,
        };
    }

    public async Task<bool> DeleteRuleAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return false;
        }

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM automation_rules WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _lastTriggeredUtc.TryRemove(id, out _);
        return affected > 0;
    }

    public async Task<IReadOnlyList<AutomationRuleAlert>> EvaluateAsync(SystemVitalsService vitals, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vitals);

        var rules = await GetRulesAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var alerts = new List<AutomationRuleAlert>();

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isTriggered = rule.TriggerType switch
            {
                AutomationTriggerType.LowDiskFreeGb => vitals.DiskFreeGb >= 0 && vitals.DiskFreeGb <= rule.Threshold,
                AutomationTriggerType.HighCpuPercent => vitals.CpuPercent >= 0 && vitals.CpuPercent >= rule.Threshold,
                AutomationTriggerType.HighRamPercent => vitals.RamUsedPercent >= 0 && vitals.RamUsedPercent >= rule.Threshold,
                _ => false,
            };

            if (!isTriggered)
            {
                continue;
            }

            if (_lastTriggeredUtc.TryGetValue(rule.Id, out var lastTriggered) &&
                now - lastTriggered < TimeSpan.FromMinutes(Math.Max(0, rule.CooldownMinutes)))
            {
                continue;
            }

            _lastTriggeredUtc[rule.Id] = now;
            alerts.Add(new AutomationRuleAlert
            {
                Rule = rule,
                TriggeredUtc = now,
                Message = BuildMessage(rule, vitals),
            });
        }

        return alerts;
    }

    private static string BuildMessage(AutomationRule rule, SystemVitalsService vitals)
    {
        return rule.TriggerType switch
        {
            AutomationTriggerType.LowDiskFreeGb =>
                $"Rule '{rule.Name}' triggered: disk free space is {vitals.DiskFreeGb:F1} GB (threshold: {rule.Threshold:F1} GB).",

            AutomationTriggerType.HighCpuPercent =>
                $"Rule '{rule.Name}' triggered: CPU is {vitals.CpuPercent:F0}% (threshold: {rule.Threshold:F0}%).",

            AutomationTriggerType.HighRamPercent =>
                $"Rule '{rule.Name}' triggered: RAM usage is {vitals.RamUsedPercent:F0}% (threshold: {rule.Threshold:F0}%).",

            _ => $"Rule '{rule.Name}' triggered.",
        };
    }

    private static DateTime ParseDate(string value)
    {
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? (parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime())
            : DateTime.UtcNow;
    }
}