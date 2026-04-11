using System.Collections.ObjectModel;
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

    public HotkeyManagerViewModel(IHotkeyService hotkeys, IUserDialogService dialogs)
    {
        _hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        SaveCommand = new RelayCommand(_ => Save());
        ResetDefaultsCommand = new RelayCommand(_ => ResetDefaults());
        ReloadCommand = new RelayCommand(_ => Reload());

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
}