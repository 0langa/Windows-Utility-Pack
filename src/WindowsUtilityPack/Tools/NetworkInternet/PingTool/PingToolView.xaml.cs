using System.Windows.Controls;
using System.Windows.Input;

namespace WindowsUtilityPack.Tools.NetworkInternet.PingTool;

public partial class PingToolView : UserControl
{
    public PingToolView()
    {
        InitializeComponent();
    }

    private void PingCountBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow only digit characters; the ViewModel already clamps the value to 1–20.
        e.Handled = !e.Text.All(char.IsDigit);
    }
}
