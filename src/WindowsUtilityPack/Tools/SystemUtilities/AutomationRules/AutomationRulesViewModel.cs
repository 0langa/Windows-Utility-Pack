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
    private string _statusMessage = "Manage and test automation rules.";

    public ObservableCollection<AutomationRule> RuleItems { get; } = [];

    public IReadOnlyList<AutomationTriggerType> TriggerTypes { get; } = Enum.GetValues<AutomationTriggerType>();

    public IReadOnlyList<AutomationActionType> ActionTypes { get; } = Enum.GetValues<AutomationActionType>();

    public AutomationRule? SelectedRule
    {
        get => _selectedRule;
        set => SetProperty(ref _selectedRule, value);
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

    public AutomationRulesViewModel(IAutomationRuleService rules, IUserDialogService dialogs)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => SelectedRule is not null);
        DeleteCommand = new AsyncRelayCommand(_ => DeleteAsync(), _ => SelectedRule is not null && SelectedRule.Id > 0);
        AddCommand = new RelayCommand(_ => AddRule());

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
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
        };

        RuleItems.Insert(0, rule);
        SelectedRule = rule;
        StatusMessage = "New rule added. Configure and click Save.";
    }

    private async Task SaveAsync()
    {
        if (SelectedRule is null)
        {
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
}