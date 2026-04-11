using System.IO;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public sealed class MainWindowXamlWiringTests
{
    [Fact]
    public void MainWindowXaml_WiresPreviewKeyDown_ForCommandPaletteKeyboardNavigation()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());

        Assert.Contains("PreviewKeyDown=\"OnPreviewKeyDown\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowXaml_WiresBackdropClick_ToDismissCommandPalette()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());

        Assert.Contains("MouseLeftButtonDown=\"OnPaletteBackdropMouseLeftButtonDown\"", xaml, StringComparison.Ordinal);
    }

    private static string GetMainWindowXamlPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "WindowsUtilityPack", "MainWindow.xaml"));
    }
}
