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

public class EnvVarsEditorViewModel : ViewModelBase, INavigationGuard
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
            if (_selectedEntry != value && !CanNavigateAway()) { OnPropertyChanged(); return; }
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
    public RelayCommand RevertCommand     { get; }
    public RelayCommand CopyValueCommand  { get; }

    private readonly IClipboardService _clipboard;
    private readonly IEnvironmentVariableStore _store;
    private readonly IUserDialogService _dialogs;

    public EnvVarsEditorViewModel(IClipboardService clipboard) : this(clipboard, new EnvironmentVariableStore(), new UserDialogService()) { }
    internal EnvVarsEditorViewModel(IClipboardService clipboard, IEnvironmentVariableStore store, IUserDialogService dialogs)
    {
        _clipboard      = clipboard;
        _store = store; _dialogs = dialogs;
        LoadCommand     = new RelayCommand(Load);
        SaveCommand     = new RelayCommand(Save);
        DeleteCommand   = new RelayCommand(Delete);
        NewCommand      = new RelayCommand(New);
        RevertCommand   = new RelayCommand(Revert);
        CopyValueCommand = new RelayCommand(CopyValue);

        Load();
    }

    private void Load()
    {
        if (!CanNavigateAway()) return;
        try
        {
            var user = _store.Read(EnvironmentVariableTarget.User);
            var machine = _store.Read(EnvironmentVariableTarget.Machine);
            IsDirty = false; SelectedEntry = null; EditName = EditValue = ""; IsDirty = false;
            AllEntries.Clear();
            AppendVariables(user, "User"); AppendVariables(machine, "Machine");
            ApplyFilter(); StatusMessage = $"Loaded {AllEntries.Count} environment variables.";
        }
        catch (Exception ex) { StatusMessage = $"Load failed: {ex.Message}"; }
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
            var name = EditName;
            EnvironmentVariableEditor.Save(_store, target, SelectedEntry?.Name, SelectedEntry?.Value, name, EditValue);
            IsDirty       = false;
            Load();
            SelectedEntry = AllEntries.FirstOrDefault(e => e.Name == name && e.Scope == scope);
            StatusMessage = $"Saved '{name}' ({scope}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private void Delete()
    {
        if (SelectedEntry == null) return;
        if (!_dialogs.Confirm("Delete variable", $"Delete '{SelectedEntry.Name}' ({SelectedEntry.Scope})?")) return;

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
            var name = SelectedEntry.Name;
            if (_store.Get(name, target) != SelectedEntry.Value) throw new InvalidOperationException("This variable changed outside the editor. Reload before deleting.");
            _store.Set(name, null, target);
            IsDirty = false;
            SelectedEntry   = null;
            Load();
            StatusMessage = $"Deleted '{name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    private void New()
    {
        if (!CanNavigateAway()) return;
        IsDirty = false;
        SelectedEntry = null;
        EditName      = string.Empty;
        EditValue     = string.Empty;
        IsDirty       = false;
        StatusMessage = "Enter a new variable name and value, then click Save.";
    }

    private void Revert()
    {
        if (SelectedEntry is not null)
        {
            EditName = SelectedEntry.Name;
            EditValue = SelectedEntry.Value;
            IsDirty = false;
            StatusMessage = "Reverted unsaved changes.";
            return;
        }

        EditName = string.Empty;
        EditValue = string.Empty;
        IsDirty = false;
        StatusMessage = "Edit fields reset.";
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
    public bool CanNavigateAway() => !IsDirty || _dialogs.Confirm("Unsaved variable", "Discard the unsaved environment variable changes?");
}
