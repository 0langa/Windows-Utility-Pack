using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace WindowsUtilityPack.Controls;

public class MenuEntry : DependencyObject
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(MenuEntry),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ToolKeyProperty =
        DependencyProperty.Register(nameof(ToolKey), typeof(string), typeof(MenuEntry),
            new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string ToolKey
    {
        get => (string)GetValue(ToolKeyProperty);
        set => SetValue(ToolKeyProperty, value);
    }
}

public partial class CategoryMenuButton : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(CategoryMenuButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(CategoryMenuButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MenuItemsProperty =
        DependencyProperty.Register(nameof(MenuItems), typeof(ObservableCollection<MenuEntry>),
            typeof(CategoryMenuButton), new PropertyMetadata(null));

    public static readonly DependencyProperty NavigateCommandProperty =
        DependencyProperty.Register(nameof(NavigateCommand), typeof(ICommand),
            typeof(CategoryMenuButton), new PropertyMetadata(null));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public ObservableCollection<MenuEntry> MenuItems
    {
        get => (ObservableCollection<MenuEntry>)GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }

    public ICommand? NavigateCommand
    {
        get => (ICommand?)GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }

    public CategoryMenuButton()
    {
        MenuItems = [];
        InitializeComponent();
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (FindName("DropdownPopup") is Popup popup)
            popup.IsOpen = true;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (FindName("DropdownPopup") is Popup popup)
        {
            var pos = e.GetPosition(this);
            if (pos.X < 0 || pos.Y < 0 || pos.X > ActualWidth || pos.Y > ActualHeight)
                popup.IsOpen = false;
        }
    }

    private void OnDropdownItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && FindName("DropdownPopup") is Popup popup)
        {
            popup.IsOpen = false;
            var toolKey = btn.Tag as string;
            if (!string.IsNullOrEmpty(toolKey) && NavigateCommand?.CanExecute(toolKey) == true)
                NavigateCommand.Execute(toolKey);
        }
    }
}
