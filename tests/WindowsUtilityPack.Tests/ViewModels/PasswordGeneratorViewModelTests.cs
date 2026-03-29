using WindowsUtilityPack.Tools.SecurityPrivacy.PasswordGenerator;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

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
            UseDigits = false,
            UseSymbols = false,
            UseUppercase = true,
            Length = 32,
        };
        Assert.True(vm.GeneratedPassword.All(char.IsUpper));
    }

    [Fact]
    public void GeneratedPassword_Empty_WhenNoCharsetSelected()
    {
        var vm = new PasswordGeneratorViewModel
        {
            UseUppercase = false,
            UseLowercase = false,
            UseDigits = false,
            UseSymbols = false,
        };
        Assert.Equal(string.Empty, vm.GeneratedPassword);
    }

    [Fact]
    public void GenerateCommand_ChangesPassword()
    {
        var vm = new PasswordGeneratorViewModel();
        var first = vm.GeneratedPassword;
        // Run generate a few times — statistically very unlikely to get same password
        var isDifferent = false;
        for (var i = 0; i < 10; i++)
        {
            vm.GenerateCommand.Execute(null);
            if (vm.GeneratedPassword != first) { isDifferent = true; break; }
        }
        Assert.True(isDifferent);
    }
}
