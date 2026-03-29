using WindowsUtilityPack.Tools.DeveloperProductivity.RegexTester;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class RegexTesterViewModelTests
{
    [Fact]
    public void Matches_PopulatedForValidPattern()
    {
        var vm = new RegexTesterViewModel
        {
            InputText = "foo 123 bar 456",
            Pattern = @"\d+",
        };
        Assert.Equal(2, vm.MatchCount);
        Assert.Equal(2, vm.Matches.Count);
        Assert.Equal("123", vm.Matches[0].Value);
        Assert.Equal("456", vm.Matches[1].Value);
    }

    [Fact]
    public void StatusMessage_ShowsError_ForInvalidPattern()
    {
        var vm = new RegexTesterViewModel
        {
            InputText = "hello",
            Pattern = "[invalid",
        };
        Assert.StartsWith("Pattern error", vm.StatusMessage);
    }

    [Fact]
    public void MatchCount_Zero_WhenNoMatches()
    {
        var vm = new RegexTesterViewModel
        {
            InputText = "hello world",
            Pattern = @"\d+",
        };
        Assert.Equal(0, vm.MatchCount);
    }
}
