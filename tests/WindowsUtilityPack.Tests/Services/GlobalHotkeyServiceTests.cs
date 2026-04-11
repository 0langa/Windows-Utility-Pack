using System.Windows.Input;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void TryParseGesture_ParsesValidGesture()
    {
        var result = GlobalHotkeyService.TryParseGesture("Ctrl+Shift+S", out var key, out var modifiers);

        Assert.True(result);
        Assert.Equal(Key.S, key);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, modifiers);
    }

    [Fact]
    public void TryParseGesture_RejectsInvalidGesture()
    {
        var result = GlobalHotkeyService.TryParseGesture("not-a-gesture", out _, out _);

        Assert.False(result);
    }
}
