using System.Collections;
using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Windows;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.EnvVarsEditor;

public class EnvVarEntry
{
    public string Name  { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty; // "User" or "Machine"
}

public class EnvVarsEditorViewModel : ViewModelBase
{
    private ObservableCollection<EnvVarEntry> _allEntries      = [];
    private ObservableCollection<EnvVarEntry> _filteredEntries = [];
    private EnvVarEntry? _selectedEntry;
    private string _searchText   = string.Empty;
    private string _scopeFilter  = "All";
    private string _editName     = string.Empty;
    private string _editValue    = string.Empty;
    private bool   _isDirty;
    private string _statusMessage = string.Empty;

    public ObservableCollection<EnvVarEntry> AllEntries
    {
        get => _allEntries;
        private set => SetProperty(ref _allEntries, value);
    }

    public ObservableCollection<EnvVarEntry> FilteredEntries
    {
        get => _filteredEntries;
        private set => SetProperty(ref _filteredEntries, value);
    }

    public EnvVarEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value) && value != null)
            {
                EditName  = value.Name;
                EditValue = value.Value;
                IsDirty   = false;
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    public string ScopeFilter
    {
        get => _scopeFilter;
        set { if (SetProperty(ref _scopeFilter, value)) ApplyFilter(); }
    }

    public string EditName
    {
        get => _editName;
        set { if (SetProperty(ref _editName, value)) IsDirty = true; }
    }

    public string EditValue
    {
        get => _editValue;
        set { if (SetProperty(ref _editValue, value)) IsDirty = true; }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand LoadCommand       { get; }
    public RelayCommand SaveCommand       { get; }
    public RelayCommand DeleteCommand     { get; }
    public RelayCommand NewCommand        { get; }
    public RelayCommand CopyValueCommand  { get; }

    private readonly IClipboardService _clipboard;

    public EnvVarsEditorViewModel(IClipboardService clipboard)
    {
        _clipboard      = clipboard;
        LoadCommand     = new RelayCommand(Load);
        SaveCommand     = new RelayCommand(Save);
        DeleteCommand   = new RelayCommand(Delete);
        NewCommand      = new RelayCommand(New);
        CopyValueCommand = new RelayCommand(CopyValue);

        Load();
    }

    private void Load()
    {
        AllEntries.Clear();

        AppendVariables(Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User),    "User");
        AppendVariables(Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine), "Machine");

        ApplyFilter();
        StatusMessage = $"Loaded {AllEntries.Count} environment variables.";
    }

    private void AppendVariables(IDictionary vars, string scope)
    {
        foreach (DictionaryEntry kv in vars)
        {
            AllEntries.Add(new EnvVarEntry
            {
                Name  = kv.Key?.ToString()   ?? string.Empty,
                Value = kv.Value?.ToString() ?? string.Empty,
                Scope = scope
            });
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        var query  = AllEntries.AsEnumerable();

        if (ScopeFilter != "All")
            query = query.Where(e => e.Scope == ScopeFilter);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(e =>
                e.Name.Contains(search,  StringComparison.OrdinalIgnoreCase) ||
                e.Value.Contains(search, StringComparison.OrdinalIgnoreCase));

        FilteredEntries = new ObservableCollection<EnvVarEntry>(query.OrderBy(e => e.Name));
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "Name cannot be empty.";
            return;
        }

        var scope = SelectedEntry?.Scope ?? (ScopeFilter == "Machine" ? "Machine" : "User");

        if (scope == "Machine" && !IsAdministrator())
        {
            StatusMessage = "Saving Machine-scope variables requires administrator privileges.";
            return;
        }

        var target = scope == "Machine"
            ? EnvironmentVariableTarget.Machine
            : EnvironmentVariableTarget.User;

        try
        {
            Environment.SetEnvironmentVariable(EditName, EditValue, target);
            StatusMessage = $"Saved '{EditName}' ({scope}).";
            IsDirty       = false;
            Load();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private void Delete()
    {
        if (SelectedEntry == null) return;

        var scope = SelectedEntry.Scope;
        if (scope == "Machine" && !IsAdministrator())
        {
            StatusMessage = "Deleting Machine-scope variables requires administrator privileges.";
            return;
        }

        var target = scope == "Machine"
            ? EnvironmentVariableTarget.Machine
            : EnvironmentVariableTarget.User;

        try
        {
            Environment.SetEnvironmentVariable(SelectedEntry.Name, null, target);
            StatusMessage   = $"Deleted '{SelectedEntry.Name}'.";
            SelectedEntry   = null;
            Load();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    private void New()
    {
        SelectedEntry = null;
        EditName      = string.Empty;
        EditValue     = string.Empty;
        IsDirty       = false;
        StatusMessage = "Enter a new variable name and value, then click Save.";
    }

    private void CopyValue()
    {
        if (!string.IsNullOrEmpty(EditValue))
        {
            _clipboard.SetText(EditValue);
            StatusMessage = "Value copied to clipboard.";
        }
    }

    private static bool IsAdministrator()
    {
        using var identity  = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
