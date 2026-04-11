using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.SystemUtilities.HotkeyManager;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class HotkeyManagerViewModelTests
{
    [Fact]
    public void Constructor_LoadsBindingsAndEnabledState()
    {
        var service = new StubHotkeyService
        {
            HotkeysEnabled = false,
            Bindings =
            [
                new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true }
            ]
        };
        var dialogs = new StubDialogService();
        var global = new StubGlobalHotkeyService
        {
            RegistrationIssues =
            [
                new HotkeyRegistrationIssue
                {
                    Action = ShellHotkeyAction.OpenCommandPalette.ToString(),
                    Gesture = "Ctrl+K",
                    Message = "Already registered by another app",
                }
            ]
        };

        var vm = new HotkeyManagerViewModel(service, dialogs, global);

        Assert.False(vm.HotkeysEnabled);
        Assert.Single(vm.Bindings);
        Assert.Single(vm.RegistrationIssues);
        Assert.Equal("Ctrl+K", vm.Bindings[0].Gesture);
        Assert.Contains("loaded", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveCommand_OnValidationError_ShowsError()
    {
        var service = new StubHotkeyService
        {
            SaveResult = (false, "collision detected")
        };
        var dialogs = new StubDialogService();
        var vm = new HotkeyManagerViewModel(service, dialogs);

        vm.SaveCommand.Execute(null);

        Assert.Contains("collision", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(dialogs.Errors);
        Assert.Empty(dialogs.Infos);
    }

    [Fact]
    public void SaveCommand_OnSuccess_ShowsInfoAndPersistsEnabledState()
    {
        var service = new StubHotkeyService
        {
            HotkeysEnabled = true,
            SaveResult = (true, string.Empty)
        };
        var dialogs = new StubDialogService();
        var vm = new HotkeyManagerViewModel(service, dialogs)
        {
            HotkeysEnabled = false
        };

        vm.SaveCommand.Execute(null);

        Assert.False(service.HotkeysEnabled);
        Assert.Contains("saved", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(dialogs.Infos);
    }

    [Fact]
    public void ResetDefaults_WhenConfirmed_ReplacesBindingList()
    {
        var service = new StubHotkeyService
        {
            Bindings =
            [
                new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true }
            ],
            Defaults =
            [
                new HotkeyBindingSetting { Action = ShellHotkeyAction.NavigateHome.ToString(), Gesture = "Ctrl+H", Enabled = true }
            ]
        };
        var dialogs = new StubDialogService { ConfirmResult = true };
        var vm = new HotkeyManagerViewModel(service, dialogs);

        vm.ResetDefaultsCommand.Execute(null);

        Assert.Single(vm.Bindings);
        Assert.Equal(ShellHotkeyAction.NavigateHome.ToString(), vm.Bindings[0].Action);
        Assert.Contains("restored", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHotkeyService : IHotkeyService
    {
        public event EventHandler? BindingsChanged;

        public bool HotkeysEnabled { get; set; } = true;
        public List<HotkeyBindingSetting> Bindings { get; set; } =
        [
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true }
        ];

        public List<HotkeyBindingSetting> Defaults { get; set; } =
        [
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenSettings.ToString(), Gesture = "Ctrl+OemComma", Enabled = true }
        ];

        public (bool Success, string Error) SaveResult { get; set; } = (true, string.Empty);

        public IReadOnlyList<HotkeyBindingSetting> GetBindings() => Bindings.Select(Clone).ToList();

        public IReadOnlyList<HotkeyBindingSetting> GetDefaultBindings() => Defaults.Select(Clone).ToList();

        public (bool Success, string Error) SaveBindings(IReadOnlyList<HotkeyBindingSetting> bindings)
        {
            if (SaveResult.Success)
            {
                Bindings = bindings.Select(Clone).ToList();
                BindingsChanged?.Invoke(this, EventArgs.Empty);
            }

            return SaveResult;
        }

        public string ExportProfileJson() => "{}";

        public (bool Success, string Error, int ImportedCount) ImportProfileJson(string json) => (true, string.Empty, 0);

        public bool TryMatch(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers, out ShellHotkeyAction action)
        {
            action = default;
            return false;
        }

        private static HotkeyBindingSetting Clone(HotkeyBindingSetting setting) => new()
        {
            Action = setting.Action,
            Gesture = setting.Gesture,
            Enabled = setting.Enabled,
        };
    }

    private sealed class StubDialogService : IUserDialogService
    {
        public bool ConfirmResult { get; set; } = true;
        public List<(string Title, string Message)> Infos { get; } = [];
        public List<(string Title, string Message)> Errors { get; } = [];

        public bool Confirm(string title, string message) => ConfirmResult;

        public void ShowInfo(string title, string message) => Infos.Add((title, message));

        public void ShowError(string title, string message) => Errors.Add((title, message));
    }

    private sealed class StubGlobalHotkeyService : IGlobalHotkeyService
    {
        public event EventHandler<ShellHotkeyAction>? HotkeyPressed
        {
            add { }
            remove { }
        }

        public event EventHandler? RegistrationsChanged
        {
            add { }
            remove { }
        }
        public bool IsStarted => true;
        public IReadOnlyList<GlobalHotkeyRegistration> ActiveRegistrations => [];
        public IReadOnlyList<HotkeyRegistrationIssue> RegistrationIssues { get; init; } = [];
        public void Start() { }
        public void Stop() { }
        public void Refresh() { }
        public void Dispose() { }
    }
}
