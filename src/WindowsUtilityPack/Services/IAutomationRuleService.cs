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

    IReadOnlyList<AutomationRuleTemplate> GetTemplates();

    AutomationRule CreateRuleFromTemplate(string templateKey);

    Task<IReadOnlyList<AutomationRuleSimulationResult>> DryRunAsync(AutomationVitalsSnapshot snapshot, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default automation rule engine.
/// </summary>
public sealed class AutomationRuleService : IAutomationRuleService
{
    /// <summary>
    /// Dispatches the specified automation action.
    /// </summary>
    public static async Task DispatchActionAsync(AutomationRule rule, CancellationToken cancellationToken = default)
    {
        try
        {
            switch (rule.ActionType)
            {
                case AutomationActionType.LaunchTool:
                    // Use rule.Name as tool key (or extend rule model for a ToolKey property if needed)
                    var toolKey = rule.Name; // This assumes rule.Name is the tool key; adjust as needed
                    var def = WindowsUtilityPack.Tools.ToolRegistry.GetByKey(toolKey);
                    if (def != null)
                    {
                        App.NavigationService.NavigateTo(toolKey);
                        App.LoggingService.LogInfo($"Automation: Launched tool '{toolKey}'");
                    }
                    else
                    {
                        App.LoggingService.LogWarning($"Automation: Tool key '{toolKey}' not found for LaunchTool action.");
                    }
                    break;
                case AutomationActionType.RunCleanup:
                    // Trigger a cleanup analysis and log (actual cleanup may require user confirmation)
                    try
                    {
                        // This is a placeholder; real cleanup would require more plumbing
                        var drives = System.IO.DriveInfo.GetDrives();
                        foreach (var drive in drives)
                        {
                            if (!drive.IsReady) continue;
                            var root = new WindowsUtilityPack.Models.StorageItem { Name = drive.Name, FullPath = drive.RootDirectory.FullName, IsDirectory = true };
                            var recs = await App.CleanupRecommendationService.AnalyseAsync(root, null, cancellationToken);
                            App.LoggingService.LogInfo($"Automation: RunCleanup found {recs.Count} recommendations on {drive.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        App.LoggingService.LogError("Automation: RunCleanup failed", ex);
                    }
                    break;
                case AutomationActionType.KillProcess:
                    // Use rule.Name as process name (or extend rule model for a ProcessName property if needed)
                    var processName = rule.Name;
                    var processes = await App.ProcessExplorerService.GetProcessesAsync(processName, cancellationToken);
                    if (processes.Count == 0)
                    {
                        App.LoggingService.LogWarning($"Automation: No process found with name '{processName}' for KillProcess action.");
                        break;
                    }
                    foreach (var proc in processes)
                    {
                        // Optionally check CPU/memory thresholds here if rule.Threshold is used for that
                        var killed = await App.ProcessExplorerService.TryTerminateAsync(proc.ProcessId, cancellationToken);
                        if (killed)
                            App.LoggingService.LogInfo($"Automation: Killed process {proc.Name} (PID {proc.ProcessId})");
                        else
                            App.LoggingService.LogWarning($"Automation: Failed to kill process {proc.Name} (PID {proc.ProcessId})");
                    }
                    break;
                case AutomationActionType.ShowNotification:
                default:
                    // Already handled elsewhere
                    break;
            }
        }
        catch (Exception ex)
        {
            App.LoggingService.LogError($"Automation: Action dispatch failed for rule '{rule.Name}'", ex);
        }
    }
    private static readonly IReadOnlyList<AutomationRuleTemplate> Templates =
    [
        new AutomationRuleTemplate
        {
            Key = "disk-critical",
            Name = "Disk space critical",
            Description = "Trigger when free disk space drops to 5 GB or less.",
            TriggerType = AutomationTriggerType.LowDiskFreeGb,
            Threshold = 5,
            CooldownMinutes = 30,
        },
        new AutomationRuleTemplate
        {
            Key = "cpu-sustained",
            Name = "CPU sustained high",
            Description = "Trigger when CPU usage reaches 85% or higher.",
            TriggerType = AutomationTriggerType.HighCpuPercent,
            Threshold = 85,
            CooldownMinutes = 15,
        },
        new AutomationRuleTemplate
        {
            Key = "ram-pressure",
            Name = "Memory pressure",
            Description = "Trigger when RAM usage reaches 90% or higher.",
            TriggerType = AutomationTriggerType.HighRamPercent,
            Threshold = 90,
            CooldownMinutes = 20,
        },
    ];

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

        // Gather additional info for new triggers
        double diskUsagePercent = -1;
        int processCount = -1;
        try
        {
            if (vitals.DiskTotalGb > 0)
                diskUsagePercent = 100.0 - (vitals.DiskFreeGb / vitals.DiskTotalGb * 100.0);
        }
        catch { }
        try
        {
            processCount = System.Diagnostics.Process.GetProcesses().Length;
        }
        catch { }

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool isTriggered = rule.TriggerType switch
            {
                AutomationTriggerType.LowDiskFreeGb => vitals.DiskFreeGb >= 0 && vitals.DiskFreeGb <= rule.Threshold,
                AutomationTriggerType.HighCpuPercent => vitals.CpuPercent >= 0 && vitals.CpuPercent >= rule.Threshold,
                AutomationTriggerType.HighRamPercent => vitals.RamUsedPercent >= 0 && vitals.RamUsedPercent >= rule.Threshold,
                AutomationTriggerType.HighDiskUsagePercent => diskUsagePercent >= 0 && diskUsagePercent >= rule.Threshold,
                AutomationTriggerType.ProcessCountExceedsLimit => processCount >= 0 && processCount > rule.Threshold,
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

    public IReadOnlyList<AutomationRuleTemplate> GetTemplates()
        => Templates;

    public AutomationRule CreateRuleFromTemplate(string templateKey)
    {
        var template = Templates.FirstOrDefault(t => t.Key.Equals(templateKey, StringComparison.OrdinalIgnoreCase));
        if (template is null)
        {
            throw new InvalidOperationException("Unknown automation template key.");
        }

        return new AutomationRule
        {
            Name = template.Name,
            TriggerType = template.TriggerType,
            Threshold = template.Threshold,
            CooldownMinutes = template.CooldownMinutes,
            Enabled = true,
            ActionType = AutomationActionType.ShowNotification,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
        };
    }

    public async Task<IReadOnlyList<AutomationRuleSimulationResult>> DryRunAsync(AutomationVitalsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var rules = await GetRulesAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<AutomationRuleSimulationResult>(rules.Count);

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var triggered = IsTriggered(rule, snapshot.DiskFreeGb, snapshot.CpuPercent, snapshot.RamUsedPercent);
            var detail = BuildSimulationDetail(rule, snapshot, triggered);

            results.Add(new AutomationRuleSimulationResult
            {
                RuleName = rule.Name,
                Triggered = triggered,
                Detail = detail,
            });
        }

        return results;
    }

    private static bool IsTriggered(AutomationRule rule, double diskFreeGb, float cpuPercent, float ramUsedPercent)
    {
        return rule.TriggerType switch
        {
            AutomationTriggerType.LowDiskFreeGb => diskFreeGb >= 0 && diskFreeGb <= rule.Threshold,
            AutomationTriggerType.HighCpuPercent => cpuPercent >= 0 && cpuPercent >= rule.Threshold,
            AutomationTriggerType.HighRamPercent => ramUsedPercent >= 0 && ramUsedPercent >= rule.Threshold,
            _ => false,
        };
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

    private static string BuildSimulationDetail(AutomationRule rule, AutomationVitalsSnapshot snapshot, bool triggered)
    {
        var stateText = triggered ? "would trigger" : "would not trigger";
        return rule.TriggerType switch
        {
            AutomationTriggerType.LowDiskFreeGb =>
                $"{stateText}: disk free {snapshot.DiskFreeGb:F1} GB vs threshold {rule.Threshold:F1} GB.",

            AutomationTriggerType.HighCpuPercent =>
                $"{stateText}: CPU {snapshot.CpuPercent:F0}% vs threshold {rule.Threshold:F0}%.",

            AutomationTriggerType.HighRamPercent =>
                $"{stateText}: RAM {snapshot.RamUsedPercent:F0}% vs threshold {rule.Threshold:F0}%.",

            _ => $"{stateText}.",
        };
    }

    private static DateTime ParseDate(string value)
    {
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? (parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime())
            : DateTime.UtcNow;
    }
}