using System.Windows.Controls;
using System.Windows.Input;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void SearchBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
            vm.IsSearchFocused = true;
    }

    private void SearchBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
            vm.IsSearchFocused = false;
    }
}
