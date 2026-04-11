using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.HotkeyManager;

/// <summary>
/// ViewModel for shell hotkey configuration.
/// </summary>
public sealed class HotkeyManagerViewModel : ViewModelBase
{
    private readonly IHotkeyService _hotkeys;
    private readonly IUserDialogService _dialogs;
    private string _statusMessage = "Configure keyboard shortcuts for core shell actions.";
    private bool _hotkeysEnabled;

    public ObservableCollection<HotkeyBindingSetting> Bindings { get; } = [];

    public bool HotkeysEnabled
    {
        get => _hotkeysEnabled;
        set => SetProperty(ref _hotkeysEnabled, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetDefaultsCommand { get; }
    public RelayCommand ReloadCommand { get; }
    public RelayCommand ExportProfileCommand { get; }
    public RelayCommand ImportProfileCommand { get; }

    public HotkeyManagerViewModel(IHotkeyService hotkeys, IUserDialogService dialogs)
    {
        _hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        SaveCommand = new RelayCommand(_ => Save());
        ResetDefaultsCommand = new RelayCommand(_ => ResetDefaults());
        ReloadCommand = new RelayCommand(_ => Reload());
        ExportProfileCommand = new RelayCommand(_ => ExportProfile());
        ImportProfileCommand = new RelayCommand(_ => ImportProfile());

        Reload();
    }

    private void Reload()
    {
        Bindings.Clear();
        foreach (var binding in _hotkeys.GetBindings())
        {
            Bindings.Add(new HotkeyBindingSetting
            {
                Action = binding.Action,
                Gesture = binding.Gesture,
                Enabled = binding.Enabled,
            });
        }

        HotkeysEnabled = _hotkeys.HotkeysEnabled;
        StatusMessage = "Hotkey bindings loaded.";
    }

    private void Save()
    {
        _hotkeys.HotkeysEnabled = HotkeysEnabled;

        var result = _hotkeys.SaveBindings(Bindings.ToList());
        if (!result.Success)
        {
            StatusMessage = result.Error;
            _dialogs.ShowError("Unable to save hotkeys", result.Error);
            return;
        }

        StatusMessage = "Hotkey configuration saved.";
        _dialogs.ShowInfo("Hotkeys saved", "Hotkey bindings were saved successfully.");
    }

    private void ResetDefaults()
    {
        if (!_dialogs.Confirm("Reset hotkeys", "Reset hotkeys to defaults?"))
        {
            return;
        }

        Bindings.Clear();
        foreach (var binding in _hotkeys.GetDefaultBindings())
        {
            Bindings.Add(new HotkeyBindingSetting
            {
                Action = binding.Action,
                Gesture = binding.Gesture,
                Enabled = binding.Enabled,
            });
        }

        StatusMessage = "Default hotkey bindings restored. Click Save to persist.";
    }

    private void ExportProfile()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export hotkey profile",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = $"hotkeys-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var json = _hotkeys.ExportProfileJson();
            File.WriteAllText(dialog.FileName, json);
            StatusMessage = "Hotkey profile exported.";
            _dialogs.ShowInfo("Hotkeys exported", "Hotkey profile was exported successfully.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to export hotkey profile.";
            _dialogs.ShowError("Hotkey export failed", ex.Message);
        }
    }

    private void ImportProfile()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import hotkey profile",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var fileInfo = new FileInfo(dialog.FileName);
            if (!fileInfo.Exists || fileInfo.Length > 1024 * 1024)
            {
                StatusMessage = "Profile file is missing or too large.";
                _dialogs.ShowError("Hotkey import failed", "Profile file is missing or exceeds 1 MB.");
                return;
            }

            var json = File.ReadAllText(dialog.FileName);
            var result = _hotkeys.ImportProfileJson(json);
            if (!result.Success)
            {
                StatusMessage = result.Error;
                _dialogs.ShowError("Hotkey import failed", result.Error);
                return;
            }

            Reload();
            StatusMessage = $"Imported {result.ImportedCount:N0} hotkey bindings.";
            _dialogs.ShowInfo("Hotkeys imported", "Hotkey profile was imported successfully.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to import hotkey profile.";
            _dialogs.ShowError("Hotkey import failed", ex.Message);
        }
    }
}