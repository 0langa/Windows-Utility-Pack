using Forms = System.Windows.Forms;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Actions exposed by the system tray context menu.
/// </summary>
public enum TrayMenuAction
{
    OpenMainWindow,
    OpenCommandPalette,
    QuickScreenshot,
    OpenScreenshotAnnotator,
    OpenClipboardManager,
    ToggleHotkeys,
    Exit,
}

/// <summary>
/// Abstraction over tray icon lifecycle and menu interactions.
/// </summary>
public interface ITrayIconService : IDisposable
{
    event EventHandler<TrayMenuAction>? ActionInvoked;

    event EventHandler? DoubleClicked;

    bool IsVisible { get; }

    void Initialize();

    void UpdateHotkeysEnabled(bool enabled);

    void ShowBalloon(string title, string message, Forms.ToolTipIcon icon);
}

/// <summary>
/// Default tray icon service implementation.
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private readonly Forms.NotifyIcon _icon = new();
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Forms.ToolStripMenuItem _toggleHotkeysItem = new("Disable Global Hotkeys");
    private bool _initialised;

    public event EventHandler<TrayMenuAction>? ActionInvoked;

    public event EventHandler? DoubleClicked;

    public bool IsVisible => _icon.Visible;

    public void Initialize()
    {
        if (_initialised)
        {
            return;
        }

        _icon.Text = "Windows Utility Pack";
        try
        {
            // Try to load the app icon from resources (Assets/WindowsUtilityPackLogo.ico)
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "WindowsUtilityPackLogo.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _icon.Icon = new System.Drawing.Icon(iconPath);
            }
            else
            {
                _icon.Icon = System.Drawing.SystemIcons.Application;
            }
        }
        catch
        {
            _icon.Icon = System.Drawing.SystemIcons.Application;
        }
        _icon.Visible = true;

        _menu.Items.Add(CreateActionItem("Open Main Window", TrayMenuAction.OpenMainWindow));
        _menu.Items.Add(CreateActionItem("Open Command Palette", TrayMenuAction.OpenCommandPalette));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(CreateActionItem("Quick Screenshot", TrayMenuAction.QuickScreenshot));
        _menu.Items.Add(CreateActionItem("Open Screenshot Annotator", TrayMenuAction.OpenScreenshotAnnotator));
        _menu.Items.Add(CreateActionItem("Open Clipboard Manager", TrayMenuAction.OpenClipboardManager));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _toggleHotkeysItem.Click += (_, _) => ActionInvoked?.Invoke(this, TrayMenuAction.ToggleHotkeys);
        _menu.Items.Add(_toggleHotkeysItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(CreateActionItem("Exit", TrayMenuAction.Exit));

        _icon.ContextMenuStrip = _menu;
        _icon.DoubleClick += (_, _) => DoubleClicked?.Invoke(this, EventArgs.Empty);
        _initialised = true;
    }

    public void UpdateHotkeysEnabled(bool enabled)
    {
        _toggleHotkeysItem.Text = enabled ? "Disable Global Hotkeys" : "Enable Global Hotkeys";
    }

    public void ShowBalloon(string title, string message, Forms.ToolTipIcon icon)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = icon;
        _icon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }

    private Forms.ToolStripMenuItem CreateActionItem(string text, TrayMenuAction action)
    {
        var item = new Forms.ToolStripMenuItem(text);
        item.Click += (_, _) => ActionInvoked?.Invoke(this, action);
        return item;
    }
}
