using System.Windows.Controls;
using System.ComponentModel;

namespace WindowsUtilityPack.Tools.SecurityPrivacy.LocalSecretVault;

public partial class LocalSecretVaultView : UserControl
{
    private bool _suppressSync;
    private LocalSecretVaultViewModel? _subscribedViewModel;

    public LocalSecretVaultView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => SyncPasswordBoxFromViewModel();
        Unloaded += (_, _) =>
        {
            if (_subscribedViewModel is not null)
            {
                _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _subscribedViewModel = null;
            }
        };
    }

    private void MasterPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LocalSecretVaultViewModel vm && sender is PasswordBox pb)
            vm.MasterPassword = pb.Password;
    }

    private void EditValuePasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_suppressSync)
        {
            return;
        }

        if (DataContext is not LocalSecretVaultViewModel vm || sender is not PasswordBox pb)
        {
            return;
        }

        if (!vm.IsValueVisible)
        {
            vm.EditValue = pb.Password;
        }
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel = null;
        }

        if (e.NewValue is LocalSecretVaultViewModel vm)
        {
            _subscribedViewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            SyncPasswordBoxFromViewModel();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocalSecretVaultViewModel.EditValue)
            or nameof(LocalSecretVaultViewModel.SelectedSecret)
            or nameof(LocalSecretVaultViewModel.IsValueVisible))
        {
            SyncPasswordBoxFromViewModel();
        }
    }

    private void SyncPasswordBoxFromViewModel()
    {
        if (DataContext is not LocalSecretVaultViewModel vm)
        {
            return;
        }

        if (EditValuePasswordBox.Password == vm.EditValue)
        {
            return;
        }

        _suppressSync = true;
        EditValuePasswordBox.Password = vm.EditValue ?? string.Empty;
        _suppressSync = false;
    }
}
