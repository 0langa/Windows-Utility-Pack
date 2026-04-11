using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class CommandPaletteServiceTests
{
    [Fact]
    public void Search_WithoutQuery_IncludesCoreShellActions()
    {
        var service = new CommandPaletteService();

        var items = service.Search(string.Empty, limit: 10);

        Assert.Contains(items, i => i.CommandKey == "open-settings");
        Assert.Contains(items, i => i.CommandKey == "home");
        Assert.Contains(items, i => i.CommandKey == "popout-current-tool");
    }

    [Fact]
    public void Search_WithQuery_FiltersByTitleAndKeywords()
    {
        var service = new CommandPaletteService();

        var items = service.Search("settings", limit: 10);

        Assert.NotEmpty(items);
        Assert.Equal("open-settings", items[0].CommandKey);
    }
}