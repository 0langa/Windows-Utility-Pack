using WindowsUtilityPack.Tools.DeveloperProductivity.RegexTester;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="RegexTesterViewModel"/>.
/// Property setters now use a debounce, so tests call <see cref="RegexTesterViewModel.RunRegex"/>
/// directly to verify the synchronous evaluation logic.
/// </summary>
public class RegexTesterViewModelTests
{
    [Fact]
    public void Matches_PopulatedForValidPattern()
    {
        var vm = new RegexTesterViewModel();
        vm.InputText = "foo 123 bar 456";
        vm.Pattern   = @"\d+";
        vm.RunRegex();

        Assert.Equal(2, vm.MatchCount);
        Assert.Equal(2, vm.Matches.Count);
        Assert.Equal("123", vm.Matches[0].Value);
        Assert.Equal("456", vm.Matches[1].Value);
    }

    [Fact]
    public void StatusMessage_ShowsError_ForInvalidPattern()
    {
        var vm = new RegexTesterViewModel();
        vm.InputText = "hello";
        vm.Pattern   = "[invalid";
        vm.RunRegex();

        Assert.StartsWith("Pattern error", vm.StatusMessage);
    }

    [Fact]
    public void MatchCount_Zero_WhenNoMatches()
    {
        var vm = new RegexTesterViewModel();
        vm.InputText = "hello world";
        vm.Pattern   = @"\d+";
        vm.RunRegex();

        Assert.Equal(0, vm.MatchCount);
    }

    [Fact]
    public void Clear_ResetsPatternAndInput()
    {
        var vm = new RegexTesterViewModel();
        vm.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, vm.InputText);
        Assert.Equal(string.Empty, vm.Pattern);
    }

    [Fact]
    public void MatchCount_Zero_WhenPatternIsEmpty()
    {
        var vm = new RegexTesterViewModel();
        vm.InputText = "some text";
        vm.Pattern   = string.Empty;
        vm.RunRegex();

        Assert.Equal(0, vm.MatchCount);
        Assert.Empty(vm.Matches);
    }

    [Fact]
    public void IgnoreCase_FindsCaseInsensitiveMatches()
    {
        var vm = new RegexTesterViewModel();
        vm.InputText   = "Hello HELLO hello";
        vm.Pattern     = "hello";
        vm.IgnoreCase  = true;
        vm.RunRegex();

        Assert.Equal(3, vm.MatchCount);
    }
}
