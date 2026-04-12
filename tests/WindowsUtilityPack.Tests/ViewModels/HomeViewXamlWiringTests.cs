using System.IO;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public sealed class HomeViewXamlWiringTests
{
    [Fact]
    public void HomeViewXaml_CategoryButton_UsesUserControlAncestorForSelectCategoryCommand()
    {
        var xaml = File.ReadAllText(GetHomeViewXamlPath());

        Assert.Contains(
            "SelectCategoryCommand, RelativeSource={RelativeSource AncestorType=UserControl}",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HomeViewXaml_RendersSelectedCategoryToolsPanel()
    {
        var xaml = File.ReadAllText(GetHomeViewXamlPath());

        Assert.Contains("ItemsSource=\"{Binding SelectedCategoryTools}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HasSelectedCategory", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeViewXaml_UsesHeaderToggleIconStyle_ForExpandableSections()
    {
        var xaml = File.ReadAllText(GetHomeViewXamlPath());

        Assert.Contains("x:Key=\"HeaderToggleIconButtonStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToggleRecommendedExpandedCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ToggleAllToolsExpandedCommand", xaml, StringComparison.Ordinal);
    }

    private static string GetHomeViewXamlPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "WindowsUtilityPack", "Views", "HomeView.xaml"));
    }
}
