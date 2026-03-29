using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace WindowsUtilityPack.Controls;

/// <summary>
/// Represents a single entry in a CategoryMenuButton's dropdown.
/// </summary>
public class MenuEntry : DependencyObject
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(MenuEntry),
            new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}

/// <summary>
/// A category navigation button that shows a stable dropdown popup on hover.
/// Add items via the MenuItems collection property.
/// </summary>
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

    public CategoryMenuButton()
    {
        MenuItems = [];
        InitializeComponent();
    }

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (FindName("DropdownPopup") is Popup popup)
            popup.IsOpen = true;
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (FindName("DropdownPopup") is Popup popup)
        {
            var pos = e.GetPosition(this);
            if (pos.X < 0 || pos.Y < 0 || pos.X > ActualWidth || pos.Y > ActualHeight)
                popup.IsOpen = false;
        }
    }
}
