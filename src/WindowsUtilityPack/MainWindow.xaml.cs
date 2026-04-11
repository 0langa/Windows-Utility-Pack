using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack;

/// <summary>
/// Code-behind for the application shell window.
///
/// The window is deliberately thin — all business logic lives in
/// <see cref="MainWindowViewModel"/>.  This file only handles:
/// <list type="bullet">
///   <item>Constructing the ViewModel with the required services.</item>
///   <item>Restoring saved window geometry from settings.</item>
///   <item>Persisting window state (size, position, theme) when the window closes.</item>
/// </list>
/// </summary>
public partial class MainWindow : Window
{
    private readonly ITrayModeCoordinator _trayCoordinator = new TrayModeCoordinator();
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _isHiddenToTray;
    private bool _explicitExitRequested;

    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainWindowViewModel(
            App.NavigationService,
            App.ThemeService,
            App.NotificationService,
            App.CommandPaletteService,
            App.ActivityLogService,
            App.ToolWindowHostService);
        DataContext = vm;

        _trayIcon = CreateTrayIcon();
        _trayIcon.DoubleClick += OnTrayOpenDoubleClick;
        if (_trayIcon.ContextMenuStrip is { } menu)
        {
            if (menu.Items.Count > 0)
            {
                menu.Items[0].Click += OnTrayOpenClick;
            }

            if (menu.Items.Count > 2)
            {
                menu.Items[2].Click += OnTrayExitClick;
            }
        }

        App.NotificationService.NotificationRequested += OnNotificationRequested;
        App.BackgroundTaskService.TaskChanged += OnBackgroundTaskChanged;

        var settings = App.TryGetSettingsService()?.Load() ?? new AppSettings();
        if (settings.RememberWindowPosition)
        {
            if (!double.IsNaN(settings.WindowLeft)) Left   = settings.WindowLeft;
            if (!double.IsNaN(settings.WindowTop))  Top    = settings.WindowTop;
            Width  = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        App.NavigationService.NavigateTo("home");
    }

    private static Forms.NotifyIcon CreateTrayIcon()
    {
        var icon = new Forms.NotifyIcon
        {
            Text = "Windows Utility Pack",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open");
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit");
        icon.ContextMenuStrip = menu;
        return icon;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Escape && vm.IsCommandPaletteOpen)
        {
            vm.CloseCommandPaletteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && vm.IsCommandPaletteOpen)
        {
            vm.ExecuteCommandPaletteItemCommand.Execute(vm.SelectedCommandPaletteItem);
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        if (!App.HotkeyService.TryMatch(key, modifiers, out var action))
        {
            return;
        }

        switch (action)
        {
            case ShellHotkeyAction.OpenCommandPalette:
                vm.OpenCommandPaletteCommand.Execute(null);
                CommandPaletteQueryBox.Focus();
                break;

            case ShellHotkeyAction.OpenSettings:
                vm.OpenSettingsCommand.Execute(null);
                break;

            case ShellHotkeyAction.NavigateHome:
                vm.NavigateHomeCommand.Execute(null);
                break;

            case ShellHotkeyAction.OpenActivityLog:
                App.NavigationService.NavigateTo("activity-log");
                break;

            case ShellHotkeyAction.OpenTaskMonitor:
                App.NavigationService.NavigateTo("background-task-monitor");
                break;
        }

        e.Handled = true;
    }

    private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var settingsService = App.TryGetSettingsService();
        var themeService = App.TryGetThemeService();
        if (settingsService is null || themeService is null)
        {
            return;
        }

        var settings = settingsService.Load();

        if (_trayCoordinator.ShouldInterceptClose(settings, _explicitExitRequested))
        {
            e.Cancel = true;
            HideToTray("Windows Utility Pack is still running in the system tray.");
            return;
        }

        settings.Theme = themeService.CurrentTheme;

        if (settings.RememberWindowPosition)
        {
            settings.WindowLeft   = Left;
            settings.WindowTop    = Top;
            settings.WindowWidth  = Width;
            settings.WindowHeight = Height;
        }

        settingsService.Save(settings);
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            return;
        }

        var settings = App.TryGetSettingsService()?.Load() ?? new AppSettings();
        if (!_trayCoordinator.ShouldHideOnMinimize(settings))
        {
            return;
        }

        HideToTray("Minimized to system tray.");
    }

    private void HideToTray(string balloonText)
    {
        _isHiddenToTray = true;
        ShowInTaskbar = false;
        Hide();

        if (_trayCoordinator.ShouldShowTrayAlert(App.TryGetSettingsService()?.Load() ?? new AppSettings(), _isHiddenToTray))
        {
            ShowTrayBalloon("Windows Utility Pack", balloonText, Forms.ToolTipIcon.Info);
        }
    }

    private void RestoreFromTray()
    {
        _isHiddenToTray = false;
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnNotificationRequested(object? sender, NotificationEventArgs e)
    {
        if (!_trayCoordinator.ShouldShowTrayAlert(App.TryGetSettingsService()?.Load() ?? new AppSettings(), _isHiddenToTray))
        {
            return;
        }

        var icon = e.Type switch
        {
            NotificationType.Error => Forms.ToolTipIcon.Error,
            NotificationType.Success => Forms.ToolTipIcon.Info,
            _ => Forms.ToolTipIcon.Info,
        };

        _ = Dispatcher.InvokeAsync(() => ShowTrayBalloon("Windows Utility Pack", e.Message, icon));
    }

    private void OnBackgroundTaskChanged(object? sender, Models.BackgroundTaskInfo e)
    {
        if (!_trayCoordinator.ShouldShowTrayAlert(App.TryGetSettingsService()?.Load() ?? new AppSettings(), _isHiddenToTray))
        {
            return;
        }

        if (!_trayCoordinator.ShouldShowTaskCompletionAlert(e))
        {
            return;
        }

        var message = _trayCoordinator.BuildTaskAlertMessage(e);
        var icon = e.State == Models.BackgroundTaskState.Failed ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info;
        _ = Dispatcher.InvokeAsync(() => ShowTrayBalloon("Background Task", message, icon));
    }

    private void ShowTrayBalloon(string title, string message, Forms.ToolTipIcon icon)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = message;
        _trayIcon.BalloonTipIcon = icon;
        _trayIcon.ShowBalloonTip(3000);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        App.NotificationService.NotificationRequested -= OnNotificationRequested;
        App.BackgroundTaskService.TaskChanged -= OnBackgroundTaskChanged;

        if (_trayIcon.ContextMenuStrip is { } menu)
        {
            if (menu.Items.Count > 0)
            {
                menu.Items[0].Click -= OnTrayOpenClick;
            }

            if (menu.Items.Count > 2)
            {
                menu.Items[2].Click -= OnTrayExitClick;
            }
        }

        _trayIcon.DoubleClick -= OnTrayOpenDoubleClick;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void OnTrayOpenClick(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(RestoreFromTray);
    }

    private void OnTrayOpenDoubleClick(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(RestoreFromTray);
    }

    private void OnTrayExitClick(object? sender, EventArgs e)
    {
        _explicitExitRequested = true;
        _ = Dispatcher.InvokeAsync(Close);
    }
}
