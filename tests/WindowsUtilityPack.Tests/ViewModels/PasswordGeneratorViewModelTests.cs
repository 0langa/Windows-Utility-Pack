using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.SecurityPrivacy.PasswordGenerator;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="PasswordGeneratorViewModel"/>.
/// Verifies password generation logic without requiring any WPF UI components.
/// </summary>
public class PasswordGeneratorViewModelTests
{
    [Fact]
    public void GeneratedPassword_HasCorrectLength()
    {
        var vm = new PasswordGeneratorViewModel(new NullClipboardService());
        vm.Length = 20;
        Assert.Equal(20, vm.GeneratedPassword.Length);
    }

    [Fact]
    public void GeneratedPassword_OnlyUppercase_WhenOnlyUppercaseSelected()
    {
        var vm = new PasswordGeneratorViewModel(new NullClipboardService())
        {
            UseLowercase = false,
            UseDigits    = false,
            UseSymbols   = false,
            UseUppercase = true,
            Length       = 32,
        };
        Assert.True(vm.GeneratedPassword.All(char.IsUpper));
    }

    [Fact]
    public void GeneratedPassword_Empty_WhenNoCharsetSelected()
    {
        // All character sets disabled → the generated password must be empty.
        var vm = new PasswordGeneratorViewModel(new NullClipboardService())
        {
            UseUppercase = false,
            UseLowercase = false,
            UseDigits    = false,
            UseSymbols   = false,
        };
        Assert.Equal(string.Empty, vm.GeneratedPassword);
    }

    [Fact]
    public void GenerateCommand_ChangesPassword()
    {
        // Statistically, at least one of 10 regenerations should differ from the first.
        var vm    = new PasswordGeneratorViewModel(new NullClipboardService());
        var first = vm.GeneratedPassword;

        var isDifferent = false;
        for (var i = 0; i < 10; i++)
        {
            vm.GenerateCommand.Execute(null);
            if (vm.GeneratedPassword != first) { isDifferent = true; break; }
        }
        Assert.True(isDifferent);
    }

    [Fact]
    public void CopyCommand_PassesGeneratedPasswordToClipboardService()
    {
        var clipboard = new CapturingClipboardService();
        var vm = new PasswordGeneratorViewModel(clipboard);

        vm.CopyCommand.Execute(null);

        Assert.Equal(vm.GeneratedPassword, clipboard.LastText);
    }

    [Fact]
    public void CopyCommand_IsDisabled_WhenPasswordIsEmpty()
    {
        var vm = new PasswordGeneratorViewModel(new NullClipboardService())
        {
            UseUppercase = false,
            UseLowercase = false,
            UseDigits    = false,
            UseSymbols   = false,
        };

        Assert.False(vm.CopyCommand.CanExecute(null));
    }

    [Fact]
    public void GeneratedPassword_ContainsAllEnabledCharClasses()
    {
        // With all 4 classes enabled, every generated password should contain
        // at least one character from each class (guaranteed coverage).
        var vm = new PasswordGeneratorViewModel(new NullClipboardService())
        {
            UseUppercase = true,
            UseLowercase = true,
            UseDigits    = true,
            UseSymbols   = true,
            Length       = 16,
        };

        // Run multiple times to reduce statistical false-positive risk.
        for (var i = 0; i < 20; i++)
        {
            vm.GenerateCommand.Execute(null);
            var pw = vm.GeneratedPassword;

            Assert.True(pw.Any(char.IsUpper),  $"Password '{pw}' missing uppercase");
            Assert.True(pw.Any(char.IsLower),  $"Password '{pw}' missing lowercase");
            Assert.True(pw.Any(char.IsDigit),  $"Password '{pw}' missing digit");
            Assert.True(pw.Any(c => !char.IsLetterOrDigit(c)), $"Password '{pw}' missing symbol");
        }
    }

    [Fact]
    public void StrengthLabel_VeryStrong_WhenAllOptionsAndLongLength()
    {
        var vm = new PasswordGeneratorViewModel(new NullClipboardService())
        {
            UseUppercase = true,
            UseLowercase = true,
            UseDigits    = true,
            UseSymbols   = true,
            Length       = 24,
        };

        Assert.Equal("Very Strong", vm.StrengthLabel);
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class NullClipboardService : IClipboardService
    {
        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }

        public void SetText(string text) { }
    }

    private sealed class CapturingClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public bool TryGetText(out string text)
        {
            text = LastText ?? string.Empty;
            return LastText is not null;
        }

        public void SetText(string text) => LastText = text;
    }
}
