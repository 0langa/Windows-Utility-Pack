using System.Windows.Controls;

namespace WindowsUtilityPack.Tools.SecurityPrivacy.LocalSecretVault;

public partial class LocalSecretVaultView : UserControl
{
    public LocalSecretVaultView() { InitializeComponent(); }

    private void MasterPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LocalSecretVaultViewModel vm && sender is PasswordBox pb)
            vm.MasterPassword = pb.Password;
    }
}
