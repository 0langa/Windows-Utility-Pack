using WindowsUtilityPack.Tools.SecurityPrivacy.PasswordGenerator;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="PasswordGeneratorViewModel"/>.
/// Verifies password generation logic without requiring any WPF UI components.
/// Note: clipboard operations are not tested here as they require a UI thread.
/// </summary>
public class PasswordGeneratorViewModelTests
{
    [Fact]
    public void GeneratedPassword_HasCorrectLength()
    {
        var vm = new PasswordGeneratorViewModel();
        vm.Length = 20;
        Assert.Equal(20, vm.GeneratedPassword.Length);
    }

    [Fact]
    public void GeneratedPassword_OnlyUppercase_WhenOnlyUppercaseSelected()
    {
        var vm = new PasswordGeneratorViewModel
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
        var vm = new PasswordGeneratorViewModel
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
        var vm    = new PasswordGeneratorViewModel();
        var first = vm.GeneratedPassword;

        var isDifferent = false;
        for (var i = 0; i < 10; i++)
        {
            vm.GenerateCommand.Execute(null);
            if (vm.GeneratedPassword != first) { isDifferent = true; break; }
        }
        Assert.True(isDifferent);
    }
}
