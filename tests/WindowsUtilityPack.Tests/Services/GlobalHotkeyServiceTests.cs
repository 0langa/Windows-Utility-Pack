using System.Windows.Input;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Tests for <see cref="IGlobalHotkeyService"/> contract behaviour.
/// Uses a stub implementation to verify state tracking logic without
/// requiring a live Win32 message loop or desktop session.
/// </summary>
public sealed class GlobalHotkeyServiceTests
{
    // ── IsRegistered / RegisteredCount ────────────────────────���───────────────

    [Fact]
    public void TryRegister_Success_ReturnsTrue_AndIsRegistered()
    {
        var svc = new StubGlobalHotkeyService();

        var (success, error) = svc.TryRegister(1, ModifierKeys.Control, Key.K);

        Assert.True(success);
        Assert.Null(error);
        Assert.True(svc.IsRegistered(1));
    }

    [Fact]
    public void TryRegister_SameIdTwice_OverwritesPrevious()
    {
        var svc = new StubGlobalHotkeyService();
        svc.TryRegister(1, ModifierKeys.Control, Key.K);
        svc.TryRegister(1, ModifierKeys.Control, Key.J);

        Assert.True(svc.IsRegistered(1));
        Assert.Equal(1, svc.RegisteredCount);
    }

    [Fact]
    public void IsRegistered_ReturnsFalse_ForUnknownId()
    {
        var svc = new StubGlobalHotkeyService();
        Assert.False(svc.IsRegistered(99));
    }

    [Fact]
    public void RegisteredCount_TracksAddAndRemove()
    {
        var svc = new StubGlobalHotkeyService();
        Assert.Equal(0, svc.RegisteredCount);

        svc.TryRegister(1, ModifierKeys.Control, Key.A);
        svc.TryRegister(2, ModifierKeys.Control, Key.B);
        Assert.Equal(2, svc.RegisteredCount);

        svc.Unregister(1);
        Assert.Equal(1, svc.RegisteredCount);
    }

    // ── Unregister ───────────────────────���────────────────────────────────────

    [Fact]
    public void Unregister_RegisteredId_ReturnsTrueAndRemoves()
    {
        var svc = new StubGlobalHotkeyService();
        svc.TryRegister(5, ModifierKeys.Control, Key.F);

        var result = svc.Unregister(5);

        Assert.True(result);
        Assert.False(svc.IsRegistered(5));
    }

    [Fact]
    public void Unregister_UnknownId_ReturnsFalse()
    {
        var svc = new StubGlobalHotkeyService();
        Assert.False(svc.Unregister(99));
    }

    // ── UnregisterAll ──────────────────────────���──────────────────────────────

    [Fact]
    public void UnregisterAll_ClearsAllRegistrations()
    {
        var svc = new StubGlobalHotkeyService();
        svc.TryRegister(1, ModifierKeys.Control, Key.A);
        svc.TryRegister(2, ModifierKeys.Control, Key.B);
        svc.TryRegister(3, ModifierKeys.Control, Key.C);

        svc.UnregisterAll();

        Assert.Equal(0, svc.RegisteredCount);
        Assert.False(svc.IsRegistered(1));
        Assert.False(svc.IsRegistered(2));
        Assert.False(svc.IsRegistered(3));
    }

    [Fact]
    public void UnregisterAll_EmptyService_DoesNotThrow()
    {
        var svc = new StubGlobalHotkeyService();
        var ex = Record.Exception(() => svc.UnregisterAll());
        Assert.Null(ex);
    }

    // ── HotkeyTriggered event ─────────────────────────��───────────────────────

    [Fact]
    public void HotkeyTriggered_Fires_WithCorrectIdModifiersAndKey()
    {
        var svc = new StubGlobalHotkeyService();
        svc.TryRegister(3, ModifierKeys.Control | ModifierKeys.Shift, Key.P);

        GlobalHotkeyEventArgs? received = null;
        svc.HotkeyTriggered += (_, e) => received = e;

        svc.SimulateTrigger(3);

        Assert.NotNull(received);
        Assert.Equal(3, received.HotkeyId);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, received.Modifiers);
        Assert.Equal(Key.P, received.Key);
    }

    [Fact]
    public void HotkeyTriggered_NotFired_ForUnregisteredId()
    {
        var svc = new StubGlobalHotkeyService();
        var fired = false;
        svc.HotkeyTriggered += (_, _) => fired = true;

        svc.SimulateTrigger(77);

        Assert.False(fired);
    }

    // ── SyncFromHotkeyService ──────────────────────────────��──────────────────

    [Fact]
    public void SyncFromHotkeyService_RegistersEnabledBindings()
    {
        var hotkeyService = new StubHotkeyService
        {
            HotkeysEnabled = true,
            Bindings =
            [
                new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true },
                new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenSettings.ToString(),       Gesture = "Ctrl+OemComma", Enabled = true },
            ]
        };

        var svc = new StubGlobalHotkeyService();
        var (registered, errors) = svc.SyncFromHotkeyService(hotkeyService);

        Assert.Equal(2, registered);
        Assert.Empty(errors);
        Assert.Equal(2, svc.RegisteredCount);
    }

    [Fact]
    public void SyncFromHotkeyService_SkipsDisabledBindings()
    {
        var hotkeyService = new StubHotkeyService
        {
            HotkeysEnabled = true,
            Bindings =
            [
                new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K",         Enabled = true },
                new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenSettings.ToString(),       Gesture = "Ctrl+OemComma",  Enabled = false },
            ]
        };

        var svc = new StubGlobalHotkeyService();
        var (registered, errors) = svc.SyncFromHotkeyService(hotkeyService);

        Assert.Equal(1, registered);
        Assert.Equal(1, svc.RegisteredCount);
    }

    [Fact]
    public void SyncFromHotkeyService_WhenHotkeysDisabled_RegistersNothing()
    {
        var hotkeyService = new StubHotkeyService
        {
            HotkeysEnabled = false,
            Bindings =
            [
                new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true }
            ]
        };

        var svc = new StubGlobalHotkeyService();
        var (registered, errors) = svc.SyncFromHotkeyService(hotkeyService);

        Assert.Equal(0, registered);
        Assert.Empty(errors);
        Assert.Equal(0, svc.RegisteredCount);
    }

    [Fact]
    public void SyncFromHotkeyService_ClearsPreviousRegistrations()
    {
        var hotkeyService = new StubHotkeyService
        {
            HotkeysEnabled = true,
            Bindings =
            [
                new HotkeyBindingSetting { Action = ShellHotkeyAction.NavigateHome.ToString(), Gesture = "Ctrl+H", Enabled = true }
            ]
        };

        var svc = new StubGlobalHotkeyService();
        svc.TryRegister(99, ModifierKeys.Control, Key.Z); // Pre-existing

        svc.SyncFromHotkeyService(hotkeyService);

        Assert.False(svc.IsRegistered(99), "Pre-existing registration should be cleared.");
    }

    [Fact]
    public void SyncFromHotkeyService_InvalidGesture_ReportsError()
    {
        var hotkeyService = new StubHotkeyService
        {
            HotkeysEnabled = true,
            Bindings =
            [
                new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "INVALID!!!GESTURE", Enabled = true }
            ]
        };

        var svc = new StubGlobalHotkeyService();
        var (registered, errors) = svc.SyncFromHotkeyService(hotkeyService);

        Assert.Equal(0, registered);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void SyncFromHotkeyService_NullArgument_Throws()
    {
        var svc = new StubGlobalHotkeyService();
        Assert.Throws<ArgumentNullException>(() => svc.SyncFromHotkeyService(null!));
    }

    // ── Disposal ─────────────────────────────��─────────────────────────────���──

    [Fact]
    public void Dispose_ClearsAllRegistrations()
    {
        var svc = new StubGlobalHotkeyService();
        svc.TryRegister(1, ModifierKeys.Control, Key.A);
        svc.TryRegister(2, ModifierKeys.Control, Key.B);

        svc.Dispose();

        Assert.Equal(0, svc.RegisteredCount);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var svc = new StubGlobalHotkeyService();
        svc.Dispose();

        var ex = Record.Exception(() => svc.Dispose());
        Assert.Null(ex);
    }

    // ── GlobalHotkeyEventArgs ─────────────────────────��───────────────────────

    [Fact]
    public void GlobalHotkeyEventArgs_StoresAllProperties()
    {
        var args = new GlobalHotkeyEventArgs(7, ModifierKeys.Alt, Key.F4);

        Assert.Equal(7, args.HotkeyId);
        Assert.Equal(ModifierKeys.Alt, args.Modifiers);
        Assert.Equal(Key.F4, args.Key);
    }

    // ── Stub helpers ────────────────────────��────────────────────────────���────

    private sealed class StubGlobalHotkeyService : IGlobalHotkeyService
    {
        private readonly Dictionary<int, (ModifierKeys Modifiers, Key Key)> _registered = new();
        private bool _disposed;

        public event EventHandler<GlobalHotkeyEventArgs>? HotkeyTriggered;

        public int RegisteredCount => _registered.Count;

        public void Attach() { /* No-op in stub — no message loop needed. */ }

        public (bool Success, string? Error) TryRegister(int id, ModifierKeys modifiers, Key key)
        {
            _registered[id] = (modifiers, key);
            return (true, null);
        }

        public bool Unregister(int id)
        {
            if (!_registered.ContainsKey(id)) return false;
            _registered.Remove(id);
            return true;
        }

        public void UnregisterAll() => _registered.Clear();

        public bool IsRegistered(int id) => _registered.ContainsKey(id);

        public (int Registered, IReadOnlyList<string> Errors) SyncFromHotkeyService(IHotkeyService hotkeyService)
        {
            ArgumentNullException.ThrowIfNull(hotkeyService);
            UnregisterAll();

            if (!hotkeyService.HotkeysEnabled) return (0, []);

            var registeredCount = 0;
            var errors = new List<string>();

            foreach (var binding in hotkeyService.GetBindings())
            {
                if (!binding.Enabled || string.IsNullOrWhiteSpace(binding.Gesture)) continue;

                if (!Enum.TryParse<ShellHotkeyAction>(binding.Action, out var action))
                {
                    errors.Add($"Unknown action: {binding.Action}");
                    continue;
                }

                if (!TryParseGesture(binding.Gesture, out var key, out var mods))
                {
                    errors.Add($"Unparseable gesture: {binding.Gesture}");
                    continue;
                }

                var id = (int)action + 1;
                _registered[id] = (mods, key);
                registeredCount++;
            }

            return (registeredCount, errors);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnregisterAll();
        }

        /// <summary>Simulates a WM_HOTKEY arriving for the given ID.</summary>
        public void SimulateTrigger(int id)
        {
            if (!_registered.TryGetValue(id, out var reg)) return;
            HotkeyTriggered?.Invoke(this, new GlobalHotkeyEventArgs(id, reg.Modifiers, reg.Key));
        }

        private static bool TryParseGesture(string gestureText, out Key key, out ModifierKeys modifiers)
        {
            key = Key.None;
            modifiers = ModifierKeys.None;
            try
            {
                var converter = new KeyGestureConverter();
                var converted = converter.ConvertFromString(gestureText);
                if (converted is not KeyGesture gesture) return false;
                key = gesture.Key;
                modifiers = gesture.Modifiers;
                return true;
            }
            catch { return false; }
        }
    }

    private sealed class StubHotkeyService : IHotkeyService
    {
        public bool HotkeysEnabled { get; set; } = true;

        public List<HotkeyBindingSetting> Bindings { get; set; } = [];

        public IReadOnlyList<HotkeyBindingSetting> GetBindings() => Bindings;

        public IReadOnlyList<HotkeyBindingSetting> GetDefaultBindings() => [];

        public (bool Success, string Error) SaveBindings(IReadOnlyList<HotkeyBindingSetting> bindings) => (true, string.Empty);

        public string ExportProfileJson() => "{}";

        public (bool Success, string Error, int ImportedCount) ImportProfileJson(string json) => (true, string.Empty, 0);

        public bool TryMatch(Key key, ModifierKeys modifiers, out ShellHotkeyAction action)
        {
            action = default;
            return false;
        }
    }
}
