using System.Windows.Input;
using System.Text.Json;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class HotkeyServiceTests
{
    // ── GetBindings ───────────────────────────────────────────────────────────

    [Fact]
    public void GetBindings_ReturnsDefaultsWhenNoneConfigured()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var bindings = service.GetBindings();

        Assert.NotEmpty(bindings);
        Assert.Contains(bindings, b => b.Action == ShellHotkeyAction.OpenCommandPalette.ToString());
    }

    [Fact]
    public void GetBindings_ReturnsClone_NotOriginal()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var b1 = service.GetBindings();
        var b2 = service.GetBindings();

        // Lists should be equal in content but not the same reference.
        Assert.Equal(b1.Count, b2.Count);
        Assert.NotSame(b1, b2);
    }

    [Fact]
    public void GetDefaultBindings_ContainsAllShellActions()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var defaults = service.GetDefaultBindings();
        var actions  = defaults.Select(b => b.Action).ToHashSet();

        foreach (var action in Enum.GetValues<ShellHotkeyAction>())
        {
            Assert.Contains(action.ToString(), actions);
        }
    }

    // ── SaveBindings ──────────────────────────────────────────────────────────

    [Fact]
    public void SaveBindings_RejectsGestureCollisions()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var result = service.SaveBindings(
        [
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenSettings.ToString(),       Gesture = "Ctrl+K", Enabled = true },
        ]);

        Assert.False(result.Success);
        Assert.Contains("collision", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveBindings_AcceptsNonCollidingBindings()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var result = service.SaveBindings(
        [
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K",         Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenSettings.ToString(),       Gesture = "Ctrl+OemComma",  Enabled = true },
        ]);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void SaveBindings_NullPayload_ReturnsFalse()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var result = service.SaveBindings(null!);

        Assert.False(result.Success);
    }

    [Fact]
    public void SaveBindings_InvalidGesture_ReturnsFalse()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var result = service.SaveBindings(
        [
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "NOT_A_GESTURE", Enabled = true }
        ]);

        Assert.False(result.Success);
    }

    [Fact]
    public void SaveBindings_DisabledBindings_DoNotCollide()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        // Two bindings with the same gesture but one disabled — should not collide.
        var result = service.SaveBindings(
        [
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true  },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenSettings.ToString(),       Gesture = "Ctrl+K", Enabled = false },
        ]);

        Assert.True(result.Success);
    }

    // ── TryMatch ──────────────────────────────────────────────────────────────

    [Fact]
    public void TryMatch_ReturnsActionForConfiguredBinding()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var result = service.TryMatch(Key.K, ModifierKeys.Control, out var action);

        Assert.True(result);
        Assert.Equal(ShellHotkeyAction.OpenCommandPalette, action);
    }

    [Fact]
    public void TryMatch_ReturnsFalse_ForUnknownGesture()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var result = service.TryMatch(Key.Z, ModifierKeys.Windows, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryMatch_ReturnsFalse_WhenHotkeysDisabled()
    {
        var settings = new StubSettingsService
        {
            Current = new AppSettings { HotkeysEnabled = false }
        };
        var service = new HotkeyService(settings);

        var result = service.TryMatch(Key.K, ModifierKeys.Control, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryMatch_AfterCustomBinding_UsesNewGesture()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        service.SaveBindings(
        [
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+Shift+P", Enabled = true }
        ]);

        // Old Ctrl+K no longer maps.
        Assert.False(service.TryMatch(Key.K, ModifierKeys.Control, out _));

        // New binding Ctrl+Shift+P should match.
        Assert.True(service.TryMatch(Key.P, ModifierKeys.Control | ModifierKeys.Shift, out var action));
        Assert.Equal(ShellHotkeyAction.OpenCommandPalette, action);
    }

    // ── HotkeysEnabled ────────────────────────────────────────────────────────

    [Fact]
    public void HotkeysEnabled_Get_ReflectsSettingsValue()
    {
        var settings = new StubSettingsService
        {
            Current = new AppSettings { HotkeysEnabled = false }
        };
        var service = new HotkeyService(settings);

        Assert.False(service.HotkeysEnabled);
    }

    [Fact]
    public void HotkeysEnabled_Set_PersistsToSettings()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        service.HotkeysEnabled = false;

        Assert.False(settings.Current.HotkeysEnabled);
    }

    // ── ExportProfileJson ─────────────────────────────────────────────────────

    [Fact]
    public void ExportProfileJson_IncludesBindingsAndEnabledState()
    {
        var settings = new StubSettingsService
        {
            Current = new AppSettings { HotkeysEnabled = false },
        };
        var service = new HotkeyService(settings);

        var json = service.ExportProfileJson();

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("HotkeysEnabled").GetBoolean());
        Assert.True(document.RootElement.GetProperty("Bindings").GetArrayLength() > 0);
    }

    [Fact]
    public void ExportProfileJson_ValidJsonOutput()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var json = service.ExportProfileJson();

        // Must parse without exception.
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    // ── ImportProfileJson ─────────────────────────────────────────────────────

    [Fact]
    public void ImportProfileJson_AppliesImportedBindingsAndEnabledState()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        const string json = """
{
  "version": 1,
  "hotkeysEnabled": false,
  "bindings": [
    { "action": "OpenCommandPalette", "gesture": "Ctrl+Shift+K", "enabled": true }
  ]
}
""";

        var result = service.ImportProfileJson(json);

        Assert.True(result.Success);
        Assert.Equal(1, result.ImportedCount);
        Assert.False(service.HotkeysEnabled);
        Assert.Collection(
            service.GetBindings(),
            binding =>
            {
                Assert.Equal("OpenCommandPalette", binding.Action);
                Assert.Equal("Ctrl+Shift+K", binding.Gesture);
                Assert.True(binding.Enabled);
            });
    }

    [Fact]
    public void ImportProfileJson_RejectsInvalidJson()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var result = service.ImportProfileJson("{ invalid");

        Assert.False(result.Success);
        Assert.Contains("parsed", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportProfileJson_EmptyPayload_ReturnsFalse()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var result = service.ImportProfileJson(string.Empty);

        Assert.False(result.Success);
    }

    [Fact]
    public void ImportProfileJson_EmptyBindings_ReturnsFalse()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        const string json = """{ "version": 1, "hotkeysEnabled": true, "bindings": [] }""";

        var result = service.ImportProfileJson(json);

        Assert.False(result.Success);
    }

    [Fact]
    public void ImportProfileJson_UnknownAction_ReturnsFalse()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        const string json = """
{
  "version": 1,
  "hotkeysEnabled": true,
  "bindings": [
    { "action": "NonExistentAction", "gesture": "Ctrl+X", "enabled": true }
  ]
}
""";

        var result = service.ImportProfileJson(json);

        Assert.False(result.Success);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void ExportThenImport_RoundTripsCorrectly()
    {
        var settings = new StubSettingsService();
        var service  = new HotkeyService(settings);

        var exported = service.ExportProfileJson();
        var imported = service.ImportProfileJson(exported);

        Assert.True(imported.Success);
        Assert.True(imported.ImportedCount > 0);
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings Current { get; set; } = new();

        public AppSettings Load() => Current;

        public void Save(AppSettings settings) => Current = settings;
    }
}
