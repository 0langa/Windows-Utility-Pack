using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack;

/// <summary>
/// Code-behind for the application shell window.
///
/// Responsibilities (UI-only):
/// <list type="bullet">
///   <item>Constructing and wiring MainWindowViewModel.</item>
///   <item>Restoring/persisting saved window geometry.</item>
///   <item>Bridging tray-icon events (show/hide/exit) to window state.</item>
///   <item>Attaching the global hotkey hook after window source is initialised.</item>
///   <item>Routing global hotkey events to ViewModel commands.</item>
/// </list>
/// All business logic stays in ViewModels and Services.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ITrayModeCoordinator _trayCoordinator = new TrayModeCoordinator();
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

        // Wire tray-service events.
        App.TrayService.ShowRequested      += OnTrayShowRequested;
        App.TrayService.ExitRequested      += OnTrayExitRequested;
        App.TrayService.QuickActionRequested += OnTrayQuickActionRequested;

        // Wire notification and background-task balloon tips.
        App.NotificationService.NotificationRequested += OnNotificationRequested;
        App.BackgroundTaskService.TaskChanged          += OnBackgroundTaskChanged;

        // Wire global hotkey events.
        App.GlobalHotkeyService.HotkeyTriggered += OnGlobalHotkeyTriggered;

        // Restore window geometry.
        var settings = App.TryGetSettingsService()?.Load() ?? new AppSettings();
        if (settings.RememberWindowPosition)
        {
            if (!double.IsNaN(settings.WindowLeft)) Left   = settings.WindowLeft;
            if (!double.IsNaN(settings.WindowTop))  Top    = settings.WindowTop;
            Width  = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        App.NavigationService.NavigateTo(settings.StartupPage);
    }

    // ── Window lifecycle ──────────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Attach the global hotkey hook now that the window's HWND is available.
        App.GlobalHotkeyService.Attach();
        var (registered, errors) = App.GlobalHotkeyService.SyncFromHotkeyService(App.HotkeyService);

        if (errors.Count > 0)
        {
            App.TryGetLoggingService()?.LogInfo(
                $"Global hotkeys: {registered} registered, {errors.Count} failed — " +
                string.Join("; ", errors));
        }

        // Initialise the tray icon with the top-5 most-used tools as quick actions.
        App.TrayService.Initialize(BuildQuickActions());

        // Keep quick actions in sync when the user opens tools.
        App.NavigationService.Navigated += OnNavigatedForTrayRefresh;
    }

    private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var settingsService = App.TryGetSettingsService();
        var themeService    = App.TryGetThemeService();
        if (settingsService is null || themeService is null) return;

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
        if (WindowState != WindowState.Minimized) return;

        var settings = App.TryGetSettingsService()?.Load() ?? new AppSettings();
        if (_trayCoordinator.ShouldHideOnMinimize(settings))
        {
            HideToTray("Minimised to system tray.");
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Detach all event listeners.
        App.NavigationService.Navigated             -= OnNavigatedForTrayRefresh;
        App.TrayService.ShowRequested               -= OnTrayShowRequested;
        App.TrayService.ExitRequested               -= OnTrayExitRequested;
        App.TrayService.QuickActionRequested        -= OnTrayQuickActionRequested;
        App.NotificationService.NotificationRequested -= OnNotificationRequested;
        App.BackgroundTaskService.TaskChanged         -= OnBackgroundTaskChanged;
        App.GlobalHotkeyService.HotkeyTriggered      -= OnGlobalHotkeyTriggered;

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    // ── Keyboard routing ──────────────────────────────────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Command-palette shortcuts handled here for in-window keys.
        if (vm.IsCommandPaletteOpen)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    vm.CloseCommandPaletteCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.Enter:
                    vm.ExecuteCommandPaletteItemCommand.Execute(vm.SelectedCommandPaletteItem);
                    e.Handled = true;
                    return;

                case Key.Down:
                    SelectNextPaletteItem(vm, +1);
                    e.Handled = true;
                    return;

                case Key.Up:
                    SelectNextPaletteItem(vm, -1);
                    e.Handled = true;
                    return;
            }
        }

        // Shell hotkeys (in-app, so they still work when the window is focused).
        var key       = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        if (!App.HotkeyService.TryMatch(key, modifiers, out var action)) return;

        DispatchShellAction(vm, action);
        e.Handled = true;
    }

    // ── Tray event handlers ───────────────────────────────────────────────────

    private void OnTrayShowRequested(object? sender, EventArgs e)
        => Dispatcher.InvokeAsync(RestoreFromTray);

    private void OnTrayExitRequested(object? sender, EventArgs e)
    {
        _explicitExitRequested = true;
        _ = Dispatcher.InvokeAsync(Close);
    }

    private void OnTrayQuickActionRequested(object? sender, string toolKey)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            RestoreFromTray();
            App.NavigationService.NavigateTo(toolKey);
        });
    }

    // ── Global hotkey handler ─────────────────────────────────────────────────

    private void OnGlobalHotkeyTriggered(object? sender, GlobalHotkeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Global hotkeys arrive on the UI thread (ComponentDispatcher).
        var action = (ShellHotkeyAction)(e.HotkeyId - 1);
        DispatchShellAction(vm, action);
    }

    // ── Notification / task balloon tips ─────────────────────────────────────

    private void OnNotificationRequested(object? sender, NotificationEventArgs e)
    {
        var settings = App.TryGetSettingsService()?.Load() ?? new AppSettings();
        if (!_trayCoordinator.ShouldShowTrayAlert(settings, _isHiddenToTray)) return;

        var icon = e.Type switch
        {
            NotificationType.Error   => TrayBalloonIcon.Error,
            NotificationType.Success => TrayBalloonIcon.Info,
            _                        => TrayBalloonIcon.Info,
        };

        _ = Dispatcher.InvokeAsync(() =>
            App.TrayService.ShowBalloon("Windows Utility Pack", e.Message, icon));
    }

    private void OnBackgroundTaskChanged(object? sender, BackgroundTaskInfo taskInfo)
    {
        var settings = App.TryGetSettingsService()?.Load() ?? new AppSettings();
        if (!_trayCoordinator.ShouldShowTrayAlert(settings, _isHiddenToTray)) return;
        if (!_trayCoordinator.ShouldShowTaskCompletionAlert(taskInfo)) return;

        var message = _trayCoordinator.BuildTaskAlertMessage(taskInfo);
        var icon    = taskInfo.State == BackgroundTaskState.Failed
            ? TrayBalloonIcon.Error
            : TrayBalloonIcon.Info;

        _ = Dispatcher.InvokeAsync(() =>
            App.TrayService.ShowBalloon("Background Task", message, icon));
    }

    // ── Navigation tracking for tray quick-action refresh ────────────────────

    private void OnNavigatedForTrayRefresh(object? sender, Type vmType)
        => App.TrayService.UpdateQuickActions(BuildQuickActions());

    // ── Private helpers ───────────────────────────────────────────────────────

    private void HideToTray(string balloonText)
    {
        _isHiddenToTray = true;
        ShowInTaskbar   = false;
        Hide();

        var settings = App.TryGetSettingsService()?.Load() ?? new AppSettings();
        if (_trayCoordinator.ShouldShowTrayAlert(settings, _isHiddenToTray))
        {
            App.TrayService.ShowBalloon("Windows Utility Pack", balloonText);
        }
    }

    private void RestoreFromTray()
    {
        _isHiddenToTray = false;
        ShowInTaskbar   = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void DispatchShellAction(MainWindowViewModel vm, ShellHotkeyAction action)
    {
        switch (action)
        {
            case ShellHotkeyAction.OpenCommandPalette:
                if (!vm.IsCommandPaletteOpen)
                {
                    RestoreFromTray();
                    vm.OpenCommandPaletteCommand.Execute(null);
                    CommandPaletteQueryBox.Focus();
                }
                break;

            case ShellHotkeyAction.OpenSettings:
                RestoreFromTray();
                vm.OpenSettingsCommand.Execute(null);
                break;

            case ShellHotkeyAction.NavigateHome:
                RestoreFromTray();
                vm.NavigateHomeCommand.Execute(null);
                break;

            case ShellHotkeyAction.OpenActivityLog:
                RestoreFromTray();
                App.NavigationService.NavigateTo("activity-log");
                break;

            case ShellHotkeyAction.OpenTaskMonitor:
                RestoreFromTray();
                App.NavigationService.NavigateTo("background-task-monitor");
                break;
        }
    }

    /// <summary>
    /// Builds quick-action items from the top-5 most-launched tools.
    /// Falls back to the first 5 registered tools when no launch data exists.
    /// </summary>
    private static IReadOnlyList<TrayQuickAction> BuildQuickActions()
    {
        const int MaxItems = 5;

        var allLaunchCounts = App.HomeDashboardService.GetAllLaunchCounts();
        var candidates = ToolRegistry.GetDisplayTools();

        var ordered = candidates
            .OrderByDescending(t => allLaunchCounts.GetValueOrDefault(t.Key, 0))
            .ThenBy(t => t.Name)
            .Take(MaxItems)
            .Select(t => new TrayQuickAction { Key = t.Key, Label = t.Name })
            .ToList();

        return ordered;
    }

    private static void SelectNextPaletteItem(MainWindowViewModel vm, int direction)
    {
        var items = vm.CommandPaletteItems;
        if (items.Count == 0) return;

        var current = vm.SelectedCommandPaletteItem;
        var idx     = current is null ? -1 : items.IndexOf(current);
        var next    = Math.Clamp(idx + direction, 0, items.Count - 1);
        vm.SelectedCommandPaletteItem = items[next];
    }
}
