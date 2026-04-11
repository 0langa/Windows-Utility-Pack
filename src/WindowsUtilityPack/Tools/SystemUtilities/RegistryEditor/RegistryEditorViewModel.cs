using System.Collections.ObjectModel;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.RegistryEditor;

/// <summary>
/// ViewModel for guarded HKCU registry editing workflows.
/// </summary>
public sealed class RegistryEditorViewModel : ViewModelBase
{
    private readonly IRegistryEditorService _service;
    private readonly IUserDialogService _dialogs;

    private string _keyPath = @"HKCU\Software";
    private string _statusMessage = "Ready.";
    private RegistryValueRow? _selectedValue;
    private string _editValueName = string.Empty;
    private string _editValueKind = "String";
    private string _editValueData = string.Empty;
    private bool _isBusy;
    private string? _selectedSubKey;

    public ObservableCollection<string> SubKeys { get; } = [];
    public ObservableCollection<RegistryValueRow> Values { get; } = [];

    public IReadOnlyList<string> SupportedKinds { get; } =
    [
        "String",
        "ExpandString",
        "DWord",
        "QWord",
        "MultiString",
        "Binary",
    ];

    public string KeyPath
    {
        get => _keyPath;
        set => SetProperty(ref _keyPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RegistryValueRow? SelectedValue
    {
        get => _selectedValue;
        set
        {
            if (!SetProperty(ref _selectedValue, value))
            {
                return;
            }

            if (value is null)
            {
                return;
            }

            EditValueName = value.Name;
            EditValueKind = value.Kind;
            EditValueData = value.DisplayData;
        }
    }

    public string EditValueName
    {
        get => _editValueName;
        set => SetProperty(ref _editValueName, value);
    }

    public string EditValueKind
    {
        get => _editValueKind;
        set => SetProperty(ref _editValueKind, value);
    }

    public string EditValueData
    {
        get => _editValueData;
        set => SetProperty(ref _editValueData, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string? SelectedSubKey
    {
        get => _selectedSubKey;
        set => SetProperty(ref _selectedSubKey, value);
    }

    public AsyncRelayCommand LoadKeyCommand { get; }
    public AsyncRelayCommand SaveValueCommand { get; }
    public AsyncRelayCommand DeleteValueCommand { get; }
    public RelayCommand NavigateSubKeyCommand { get; }
    public AsyncRelayCommand BackupCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }

    public RegistryEditorViewModel(IRegistryEditorService service, IUserDialogService dialogs)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        LoadKeyCommand = new AsyncRelayCommand(_ => LoadKeyAsync());
        SaveValueCommand = new AsyncRelayCommand(_ => SaveValueAsync());
        DeleteValueCommand = new AsyncRelayCommand(_ => DeleteValueAsync());
        NavigateSubKeyCommand = new RelayCommand(_ => NavigateToSubKey(SelectedSubKey));
        BackupCommand = new AsyncRelayCommand(_ => BackupAsync());
        RestoreCommand = new AsyncRelayCommand(_ => RestoreAsync());

        _ = LoadKeyAsync();
    }

    internal async Task LoadKeyAsync()
    {
        IsBusy = true;
        try
        {
            var subKeys = await _service.GetSubKeyNamesAsync(KeyPath).ConfigureAwait(true);
            var values = await _service.GetValuesAsync(KeyPath).ConfigureAwait(true);

            SubKeys.Clear();
            foreach (var subKey in subKeys)
            {
                SubKeys.Add(subKey);
            }

            Values.Clear();
            foreach (var value in values)
            {
                Values.Add(value);
            }

            StatusMessage = $"Loaded {Values.Count:N0} values and {SubKeys.Count:N0} subkeys.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to load registry key.";
            _dialogs.ShowError("Registry Editor", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task SaveValueAsync()
    {
        if (string.IsNullOrWhiteSpace(EditValueName) && EditValueName != "(Default)")
        {
            _dialogs.ShowError("Registry Editor", "Value name is required.");
            return;
        }

        IsBusy = true;
        try
        {
            await _service.SetValueAsync(KeyPath, EditValueName, EditValueData, EditValueKind).ConfigureAwait(true);
            StatusMessage = "Value saved.";
            await LoadKeyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to save value.";
            _dialogs.ShowError("Registry Editor", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task DeleteValueAsync()
    {
        if (string.IsNullOrWhiteSpace(EditValueName) && EditValueName != "(Default)")
        {
            return;
        }

        if (!_dialogs.Confirm("Delete value", $"Delete value '{EditValueName}' from '{KeyPath}'?"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _service.DeleteValueAsync(KeyPath, EditValueName).ConfigureAwait(true);
            StatusMessage = "Value deleted.";
            await LoadKeyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to delete value.";
            _dialogs.ShowError("Registry Editor", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NavigateToSubKey(string? subKeyName)
    {
        if (string.IsNullOrWhiteSpace(subKeyName))
        {
            return;
        }

        KeyPath = KeyPath.TrimEnd('\\') + "\\" + subKeyName;
        _ = LoadKeyAsync();
    }

    private async Task BackupAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export registry backup",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = "registry-backup.json",
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _service.BackupAsync(KeyPath, dialog.FileName).ConfigureAwait(true);
            StatusMessage = "Backup exported.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Backup export failed.";
            _dialogs.ShowError("Registry Editor", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestoreAsync()
    {
        if (!_dialogs.Confirm("Restore backup", "Restore registry backup from JSON file?"))
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select registry backup",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _service.RestoreAsync(dialog.FileName).ConfigureAwait(true);
            StatusMessage = "Backup restored.";
            await LoadKeyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Backup restore failed.";
            _dialogs.ShowError("Registry Editor", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}