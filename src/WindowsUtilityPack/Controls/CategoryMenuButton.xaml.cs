using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace WindowsUtilityPack.Controls;

/// <summary>
/// A single entry in a <see cref="CategoryMenuButton"/> dropdown.
/// Carries the display label and the tool navigation key.
/// </summary>
public class MenuEntry : DependencyObject
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(MenuEntry),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ToolKeyProperty =
        DependencyProperty.Register(nameof(ToolKey), typeof(string), typeof(MenuEntry),
            new PropertyMetadata(string.Empty));

    /// <summary>Display text shown in the dropdown list.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// Navigation key passed to <c>NavigateCommand</c> when this item is clicked.
    /// Empty string means the tool is not yet implemented; clicking does nothing.
    /// </summary>
    public string ToolKey
    {
        get => (string)GetValue(ToolKeyProperty);
        set => SetValue(ToolKeyProperty, value);
    }
}

/// <summary>
/// A navigation bar button that displays a category icon + label and shows a
/// hover dropdown containing <see cref="MenuEntry"/> items.
///
/// How it works:
/// <list type="bullet">
///   <item>The control is a <see cref="UserControl"/> with a <see cref="Popup"/> child.</item>
///   <item>Hovering over the control opens the popup (<see cref="OnMouseEnter"/>).</item>
///   <item>Moving the mouse outside the control bounds closes it (<see cref="OnMouseLeave"/>).</item>
///   <item>Clicking a menu item closes the popup and fires <see cref="NavigateCommand"/>
///         with the item's <see cref="MenuEntry.ToolKey"/> as the parameter.</item>
///   <item>Items with an empty <c>ToolKey</c> are silently ignored (placeholder entries).</item>
/// </list>
/// </summary>
public partial class CategoryMenuButton : UserControl
{
    // ── Dependency properties ─────────────────────────────────────────────────

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

    // ── CLR wrappers ──────────────────────────────────────────────────────────

    /// <summary>Category label displayed below the icon.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Emoji or icon character displayed above the label.</summary>
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>The dropdown items shown when hovering over this button.</summary>
    public ObservableCollection<MenuEntry> MenuItems
    {
        get => (ObservableCollection<MenuEntry>)GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }

    /// <summary>
    /// Command invoked with the clicked item's <see cref="MenuEntry.ToolKey"/> as parameter.
    /// Typically bound to <c>MainWindowViewModel.NavigateCommand</c>.
    /// </summary>
    public ICommand? NavigateCommand
    {
        get => (ICommand?)GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public CategoryMenuButton()
    {
        // Initialise the collection before InitializeComponent so XAML can bind to it.
        MenuItems = [];
        InitializeComponent();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (FindName("DropdownPopup") is Popup popup)
            popup.IsOpen = true;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
        => Dispatcher.BeginInvoke(CloseIfMouseGone, System.Windows.Threading.DispatcherPriority.Input);

    // Called when the mouse leaves the popup's content border.
    private void OnPopupMouseLeave(object sender, MouseEventArgs e)
        => Dispatcher.BeginInvoke(CloseIfMouseGone, System.Windows.Threading.DispatcherPriority.Input);

    // Closes the popup only when the cursor is genuinely outside both the control and the popup.
    // Runs on the Input dispatcher priority so IsMouseOver is already up-to-date.
    private void CloseIfMouseGone()
    {
        if (FindName("DropdownPopup") is not Popup popup || !popup.IsOpen)
            return;

        if (IsMouseOver)
            return;

        if (popup.Child is UIElement popupContent && popupContent.IsMouseOver)
            return;

        popup.IsOpen = false;
    }

    private void OnDropdownItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && FindName("DropdownPopup") is Popup popup)
        {
            popup.IsOpen = false;

            // Skip placeholder entries that have no registered tool yet.
            var toolKey = btn.Tag as string;
            if (!string.IsNullOrEmpty(toolKey) && NavigateCommand?.CanExecute(toolKey) == true)
                NavigateCommand.Execute(toolKey);
        }
    }
}
