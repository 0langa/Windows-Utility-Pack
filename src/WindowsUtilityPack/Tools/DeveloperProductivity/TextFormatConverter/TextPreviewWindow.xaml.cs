using System.Windows;
using WindowsUtilityPack.Services.TextConversion;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;

/// <summary>
/// Modeless preview window for the conversion result.
/// </summary>
public partial class TextPreviewWindow : Window, ITextPreviewWindowHost
{
    public TextPreviewWindow()
    {
        InitializeComponent();
    }
}
