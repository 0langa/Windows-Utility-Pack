using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WindowsUtilityPack.Models;

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
/// hover/keyboard dropdown containing tool items.
///
/// Supports two modes of populating the dropdown:
/// <list type="bullet">
///   <item><see cref="ToolDefinitions"/>: data-driven from <see cref="ToolDefinition"/> list (preferred).</item>
///   <item><see cref="MenuItems"/>: legacy XAML-declared <see cref="MenuEntry"/> items.</item>
/// </list>
///
/// The dropdown opens on hover or keyboard (Enter/Space), and closes when
/// focus and pointer leave the control.
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

    public static readonly DependencyProperty ToolDefinitionsProperty =
        DependencyProperty.Register(nameof(ToolDefinitions), typeof(IReadOnlyList<ToolDefinition>),
            typeof(CategoryMenuButton), new PropertyMetadata(null, OnToolDefinitionsChanged));

    // ── CLR wrappers ──────────────────────────────────────────────────────────

    /// <summary>Category label displayed below the icon.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Segoe MDL2 Assets glyph character displayed above the label.</summary>
    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Legacy dropdown items (used when declared in XAML directly).</summary>
    public ObservableCollection<MenuEntry> MenuItems
    {
        get => (ObservableCollection<MenuEntry>)GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }

    /// <summary>
    /// Command invoked with the clicked item's tool key as parameter.
    /// Typically bound to <c>MainWindowViewModel.NavigateCommand</c>.
    /// </summary>
    public ICommand? NavigateCommand
    {
        get => (ICommand?)GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }

    /// <summary>
    /// Data-driven tool definitions from <see cref="ToolRegistry"/>.
    /// When set, automatically populates <see cref="MenuItems"/>.
    /// </summary>
    public IReadOnlyList<ToolDefinition>? ToolDefinitions
    {
        get => (IReadOnlyList<ToolDefinition>?)GetValue(ToolDefinitionsProperty);
        set => SetValue(ToolDefinitionsProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public CategoryMenuButton()
    {
        // Initialise the collection before InitializeComponent so XAML can bind to it.
        MenuItems = [];
        InitializeComponent();

        // Enable keyboard focus for accessibility.
        Focusable = true;
        KeyDown += OnKeyDown;
    }

    // ── Callback for data-driven tool definitions ─────────────────────────────

    private static void OnToolDefinitionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CategoryMenuButton self) return;

        self.MenuItems.Clear();
        if (e.NewValue is IReadOnlyList<ToolDefinition> tools)
        {
            foreach (var tool in tools)
            {
                self.MenuItems.Add(new MenuEntry { Label = tool.Name, ToolKey = tool.Key });
            }
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        DropdownPopup.IsOpen = true;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
        => Dispatcher.BeginInvoke(CloseIfNotActive, System.Windows.Threading.DispatcherPriority.Input);

    // Called when the mouse leaves the popup's content border.
    private void OnPopupMouseLeave(object sender, MouseEventArgs e)
        => Dispatcher.BeginInvoke(CloseIfNotActive, System.Windows.Threading.DispatcherPriority.Input);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space or Key.Down)
        {
            DropdownPopup.IsOpen = true;
            e.Handled = true;

            // Move focus to first dropdown item for keyboard navigation.
            Dispatcher.BeginInvoke(() =>
            {
                if (DropdownPopup.Child is FrameworkElement popupContent)
                    popupContent.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
        else if (e.Key == Key.Escape && DropdownPopup.IsOpen)
        {
            DropdownPopup.IsOpen = false;
            Focus();
            e.Handled = true;
        }
    }

    // Closes the popup only when the cursor is genuinely outside both the control and the popup,
    // and the control does not have keyboard focus inside.
    private void CloseIfNotActive()
    {
        if (!DropdownPopup.IsOpen)
            return;

        if (IsMouseOver)
            return;

        if (DropdownPopup.Child is UIElement popupContent && popupContent.IsMouseOver)
            return;

        // Keep open if keyboard focus is inside the popup.
        if (DropdownPopup.Child is UIElement popup && popup.IsKeyboardFocusWithin)
            return;

        DropdownPopup.IsOpen = false;
    }

    private void OnDropdownItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            DropdownPopup.IsOpen = false;

            // Skip placeholder entries that have no registered tool yet.
            var toolKey = btn.Tag as string;
            if (!string.IsNullOrEmpty(toolKey) && NavigateCommand?.CanExecute(toolKey) == true)
                NavigateCommand.Execute(toolKey);
        }
    }
}
