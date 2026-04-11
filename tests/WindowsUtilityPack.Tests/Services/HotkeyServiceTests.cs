using System.Windows.Input;
using System.Text.Json;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class HotkeyServiceTests
{
    [Fact]
    public void GetBindings_ReturnsDefaultsWhenNoneConfigured()
    {
        var settings = new StubSettingsService();
        var service = new HotkeyService(settings);

        var bindings = service.GetBindings();

        Assert.NotEmpty(bindings);
        Assert.Contains(bindings, b => b.Action == ShellHotkeyAction.OpenCommandPalette.ToString());
    }

    [Fact]
    public void SaveBindings_RejectsGestureCollisions()
    {
        var settings = new StubSettingsService();
        var service = new HotkeyService(settings);

        var result = service.SaveBindings(
        [
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenSettings.ToString(), Gesture = "Ctrl+K", Enabled = true },
        ]);

        Assert.False(result.Success);
        Assert.Contains("collision", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMatch_ReturnsActionForConfiguredBinding()
    {
        var settings = new StubSettingsService();
        var service = new HotkeyService(settings);

        var result = service.TryMatch(Key.K, ModifierKeys.Control, out var action);

        Assert.True(result);
        Assert.Equal(ShellHotkeyAction.OpenCommandPalette, action);
    }

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
    public void ImportProfileJson_AppliesImportedBindingsAndEnabledState()
    {
        var settings = new StubSettingsService();
        var service = new HotkeyService(settings);

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
        var service = new HotkeyService(settings);

        var result = service.ImportProfileJson("{ invalid");

        Assert.False(result.Success);
        Assert.Contains("parsed", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings Current { get; set; } = new();

        public AppSettings Load() => Current;

        public void Save(AppSettings settings)
        {
            Current = settings;
        }
    }
}