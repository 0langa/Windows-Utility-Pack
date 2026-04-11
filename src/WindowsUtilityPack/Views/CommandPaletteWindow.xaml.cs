using System.Windows;
using System.Windows.Input;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Views;

/// <summary>
/// Detached command palette window host.
/// </summary>
public partial class CommandPaletteWindow : Window
{
    public CommandPaletteWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => QueryBox.Focus();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CommandPaletteWindowViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            vm.RequestExecuteSelected();
            e.Handled = true;
        }
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CommandPaletteWindowViewModel vm)
        {
            vm.RequestExecuteSelected();
        }
    }
}
