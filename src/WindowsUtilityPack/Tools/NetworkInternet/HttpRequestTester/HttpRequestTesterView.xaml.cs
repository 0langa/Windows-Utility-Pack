using System.Windows.Controls;

namespace WindowsUtilityPack.Tools.NetworkInternet.HttpRequestTester;

public partial class HttpRequestTesterView : UserControl
{
    public HttpRequestTesterView() { InitializeComponent(); }

    private void BasicAuthPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is HttpRequestTesterViewModel vm)
            vm.AuthPassword = ((PasswordBox)sender).Password;
    }
}
