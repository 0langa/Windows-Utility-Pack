using System.Collections.ObjectModel;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.AutomationRules;

/// <summary>
/// ViewModel for automation rule management.
/// </summary>
public sealed class AutomationRulesViewModel : ViewModelBase
{
    private readonly IAutomationRuleService _rules;
    private readonly IUserDialogService _dialogs;

    private AutomationRule? _selectedRule;
    private AutomationRuleTemplate? _selectedTemplate;
    private string _simulationDiskFreeGb = "20";
    private string _simulationCpuPercent = "50";
    private string _simulationRamPercent = "60";
    private string _statusMessage = "Manage and test automation rules.";

    public ObservableCollection<AutomationRule> RuleItems { get; } = [];
    public ObservableCollection<AutomationRuleTemplate> Templates { get; } = [];
    public ObservableCollection<AutomationRuleSimulationResult> SimulationResults { get; } = [];

    public IReadOnlyList<AutomationTriggerType> TriggerTypes { get; } = Enum.GetValues<AutomationTriggerType>();

    public IReadOnlyList<AutomationActionType> ActionTypes { get; } = Enum.GetValues<AutomationActionType>();

    public AutomationRule? SelectedRule
    {
        get => _selectedRule;
        set => SetProperty(ref _selectedRule, value);
    }

    public AutomationRuleTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    public string SimulationDiskFreeGb
    {
        get => _simulationDiskFreeGb;
        set => SetProperty(ref _simulationDiskFreeGb, value);
    }

    public string SimulationCpuPercent
    {
        get => _simulationCpuPercent;
        set => SetProperty(ref _simulationCpuPercent, value);
    }

    public string SimulationRamPercent
    {
        get => _simulationRamPercent;
        set => SetProperty(ref _simulationRamPercent, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand AddFromTemplateCommand { get; }
    public AsyncRelayCommand RunDryRunCommand { get; }

    public AutomationRulesViewModel(IAutomationRuleService rules, IUserDialogService dialogs)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => SelectedRule is not null);
        DeleteCommand = new AsyncRelayCommand(_ => DeleteAsync(), _ => SelectedRule is not null && SelectedRule.Id > 0);
        AddCommand = new RelayCommand(_ => AddRule());
        AddFromTemplateCommand = new RelayCommand(_ => AddFromTemplate(), _ => SelectedTemplate is not null);
        RunDryRunCommand = new AsyncRelayCommand(_ => RunDryRunAsync());

        foreach (var template in _rules.GetTemplates())
        {
            Templates.Add(template);
        }

        if (Templates.Count > 0)
        {
            SelectedTemplate = Templates[0];
        }

        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var rules = await _rules.GetRulesAsync().ConfigureAwait(true);

        RuleItems.Clear();
        foreach (var rule in rules)
        {
            RuleItems.Add(rule);
        }

        StatusMessage = RuleItems.Count == 0
            ? "No automation rules configured."
            : $"Loaded {RuleItems.Count:N0} automation rules.";
    }

    private void AddRule()
    {
        var rule = new AutomationRule
        {
            Name = "New rule",
            TriggerType = AutomationTriggerType.LowDiskFreeGb,
            Threshold = 5,
            CooldownMinutes = 15,
            Enabled = true,
            ActionType = AutomationActionType.ShowNotification,
            ActionTarget = string.Empty,
            ActionParametersJson = "{}",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
        };

        RuleItems.Insert(0, rule);
        SelectedRule = rule;
        StatusMessage = "New rule added. Configure and click Save.";
    }

    private void AddFromTemplate()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        var rule = _rules.CreateRuleFromTemplate(SelectedTemplate.Key);
        RuleItems.Insert(0, rule);
        SelectedRule = rule;
        StatusMessage = $"Template '{SelectedTemplate.Name}' added. Review and click Save.";
    }

    private async Task SaveAsync()
    {
        if (SelectedRule is null)
        {
            return;
        }

        if (!ValidateActionTarget(SelectedRule, out var validationError))
        {
            StatusMessage = validationError;
            return;
        }

        var saved = await _rules.SaveRuleAsync(SelectedRule).ConfigureAwait(true);
        SelectedRule = saved;

        await RefreshAsync().ConfigureAwait(true);
        SelectedRule = RuleItems.FirstOrDefault(r => r.Id == saved.Id);

        StatusMessage = $"Saved rule '{saved.Name}'.";
    }

    private async Task DeleteAsync()
    {
        if (SelectedRule is null)
        {
            return;
        }

        if (!_dialogs.Confirm("Delete automation rule", $"Delete rule '{SelectedRule.Name}'?"))
        {
            return;
        }

        var name = SelectedRule.Name;
        var removed = await _rules.DeleteRuleAsync(SelectedRule.Id).ConfigureAwait(true);
        if (!removed)
        {
            StatusMessage = "Unable to delete selected rule.";
            return;
        }

        SelectedRule = null;
        await RefreshAsync().ConfigureAwait(true);
        StatusMessage = $"Deleted rule '{name}'.";
    }

    private async Task RunDryRunAsync()
    {
        if (!TryParseSimulationSnapshot(out var snapshot, out var parseError))
        {
            StatusMessage = parseError;
            return;
        }

        var results = await _rules.DryRunAsync(snapshot).ConfigureAwait(true);
        SimulationResults.Clear();
        foreach (var result in results)
        {
            SimulationResults.Add(result);
        }

        var triggered = SimulationResults.Count(r => r.Triggered);
        StatusMessage = $"Dry-run complete: {triggered:N0} of {SimulationResults.Count:N0} rules would trigger.";
    }

    private static bool ValidateActionTarget(AutomationRule rule, out string message)
    {
        if (rule.ActionType is AutomationActionType.LaunchTool or AutomationActionType.KillProcess
            && string.IsNullOrWhiteSpace(rule.ActionTarget))
        {
            message = rule.ActionType == AutomationActionType.LaunchTool
                ? "LaunchTool rules require an action target (tool key)."
                : "KillProcess rules require an action target (process name).";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool TryParseSimulationSnapshot(out AutomationVitalsSnapshot snapshot, out string error)
    {
        snapshot = new AutomationVitalsSnapshot();
        error = string.Empty;

        if (!double.TryParse(SimulationDiskFreeGb, out var disk) || disk < 0)
        {
            error = "Simulation disk free value must be a non-negative number.";
            return false;
        }

        if (!float.TryParse(SimulationCpuPercent, out var cpu) || cpu < 0 || cpu > 100)
        {
            error = "Simulation CPU value must be between 0 and 100.";
            return false;
        }

        if (!float.TryParse(SimulationRamPercent, out var ram) || ram < 0 || ram > 100)
        {
            error = "Simulation RAM value must be between 0 and 100.";
            return false;
        }

        snapshot = new AutomationVitalsSnapshot
        {
            DiskFreeGb = disk,
            CpuPercent = cpu,
            RamUsedPercent = ram,
        };

        return true;
    }
}
