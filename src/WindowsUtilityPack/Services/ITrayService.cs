using Forms = System.Windows.Forms;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Icon type for tray balloon notifications.
/// </summary>
public enum TrayBalloonIcon
{
    None,
    Info,
    Warning,
    Error,
}

/// <summary>
/// Represents a quick-action item shown in the tray context menu below the separator.
/// </summary>
public sealed class TrayQuickAction
{
    /// <summary>Navigation tool key (used with <c>INavigationService.NavigateTo</c>).</summary>
    public required string Key { get; init; }

    /// <summary>Display label shown in the context menu.</summary>
    public required string Label { get; init; }
}

/// <summary>
/// Manages the system tray icon lifecycle, context menu, and balloon notifications.
/// All methods must be called from the UI thread.
/// </summary>
public interface ITrayService : IDisposable
{
    /// <summary>Raised when the user double-clicks the tray icon or clicks "Open" in the menu.</summary>
    event EventHandler? ShowRequested;

    /// <summary>Raised when the user clicks "Exit" in the tray context menu.</summary>
    event EventHandler? ExitRequested;

    /// <summary>
    /// Raised when a quick-action item is clicked. The event argument is the tool key.
    /// </summary>
    event EventHandler<string>? QuickActionRequested;

    /// <summary>Whether the tray icon is currently visible in the notification area.</summary>
    bool IsVisible { get; }

    /// <summary>
    /// Creates and shows the tray icon with an optional initial set of quick-action items.
    /// Safe to call multiple times — subsequent calls update the quick actions and are no-ops otherwise.
    /// </summary>
    void Initialize(IReadOnlyList<TrayQuickAction>? quickActions = null);

    /// <summary>
    /// Replaces the current quick-action menu items with a new list.
    /// No-op if not yet initialized.
    /// </summary>
    void UpdateQuickActions(IReadOnlyList<TrayQuickAction> quickActions);

    /// <summary>
    /// Shows a tray balloon notification.
    /// No-op if the icon is not yet initialized.
    /// </summary>
    void ShowBalloon(string title, string message, TrayBalloonIcon icon = TrayBalloonIcon.Info);

    /// <summary>Makes the tray icon visible in the notification area.</summary>
    void Show();

    /// <summary>Hides the tray icon from the notification area.</summary>
    void Hide();
}

/// <summary>
/// Production implementation of <see cref="ITrayService"/> backed by
/// <see cref="Forms.NotifyIcon"/>.
/// </summary>
public sealed class TrayService : ITrayService
{
    private Forms.NotifyIcon? _icon;
    private bool _initialized;
    private bool _disposed;

    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<string>? QuickActionRequested;

    public bool IsVisible => _icon?.Visible ?? false;

    /// <inheritdoc/>
    public void Initialize(IReadOnlyList<TrayQuickAction>? quickActions = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_initialized)
        {
            // Re-entry: just refresh quick actions.
            if (quickActions is not null)
            {
                UpdateQuickActions(quickActions);
            }

            return;
        }

        _icon = new Forms.NotifyIcon
        {
            Text = "Windows Utility Pack",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
        };

        _icon.DoubleClick += OnIconDoubleClick;
        _icon.BalloonTipClicked += OnBalloonTipClicked;

        RebuildContextMenu(quickActions ?? []);

        _initialized = true;
    }

    /// <inheritdoc/>
    public void UpdateQuickActions(IReadOnlyList<TrayQuickAction> quickActions)
    {
        if (!_initialized || _icon is null)
        {
            return;
        }

        RebuildContextMenu(quickActions);
    }

    /// <inheritdoc/>
    public void ShowBalloon(string title, string message, TrayBalloonIcon icon = TrayBalloonIcon.Info)
    {
        if (!_initialized || _icon is null)
        {
            return;
        }

        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = MapBalloonIcon(icon);
        _icon.ShowBalloonTip(3000);
    }

    /// <inheritdoc/>
    public void Show()
    {
        if (_icon is not null)
        {
            _icon.Visible = true;
        }
    }

    /// <inheritdoc/>
    public void Hide()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_icon is not null)
        {
            _icon.DoubleClick -= OnIconDoubleClick;
            _icon.BalloonTipClicked -= OnBalloonTipClicked;

            if (_icon.ContextMenuStrip is { } menu)
            {
                foreach (Forms.ToolStripItem item in menu.Items)
                {
                    item.Click -= OnOpenClick;
                    item.Click -= OnExitClick;
                }

                menu.Dispose();
            }

            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void RebuildContextMenu(IReadOnlyList<TrayQuickAction> quickActions)
    {
        if (_icon is null)
        {
            return;
        }

        var oldMenu = _icon.ContextMenuStrip;

        // Detach old click handlers before replacing.
        if (oldMenu is not null)
        {
            foreach (Forms.ToolStripItem item in oldMenu.Items)
            {
                item.Click -= OnOpenClick;
                item.Click -= OnExitClick;
            }

            oldMenu.Dispose();
        }

        var menu = new Forms.ContextMenuStrip();

        var openItem = new Forms.ToolStripMenuItem("Open Windows Utility Pack");
        openItem.Click += OnOpenClick;
        menu.Items.Add(openItem);

        // Quick-action shortcuts (top tools).
        if (quickActions.Count > 0)
        {
            menu.Items.Add(new Forms.ToolStripSeparator());

            foreach (var action in quickActions)
            {
                var item = new Forms.ToolStripMenuItem(action.Label);
                // Capture key to avoid closure capturing loop variable.
                var toolKey = action.Key;
                item.Click += (_, _) => QuickActionRequested?.Invoke(this, toolKey);
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += OnExitClick;
        menu.Items.Add(exitItem);

        _icon.ContextMenuStrip = menu;
    }

    private void OnIconDoubleClick(object? sender, EventArgs e)
        => ShowRequested?.Invoke(this, EventArgs.Empty);

    private void OnBalloonTipClicked(object? sender, EventArgs e)
        => ShowRequested?.Invoke(this, EventArgs.Empty);

    private void OnOpenClick(object? sender, EventArgs e)
        => ShowRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitClick(object? sender, EventArgs e)
        => ExitRequested?.Invoke(this, EventArgs.Empty);

    private static Forms.ToolTipIcon MapBalloonIcon(TrayBalloonIcon icon) => icon switch
    {
        TrayBalloonIcon.Warning => Forms.ToolTipIcon.Warning,
        TrayBalloonIcon.Error   => Forms.ToolTipIcon.Error,
        TrayBalloonIcon.Info    => Forms.ToolTipIcon.Info,
        _                       => Forms.ToolTipIcon.None,
    };
}
