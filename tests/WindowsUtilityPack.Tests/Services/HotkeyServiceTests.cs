using System.Windows.Input;
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

    private sealed class StubSettingsService : ISettingsService
    {
        private AppSettings _settings = new();

        public AppSettings Load() => _settings;

        public void Save(AppSettings settings)
        {
            _settings = settings;
        }
    }
}