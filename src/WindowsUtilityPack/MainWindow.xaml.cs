using System.Windows;
using System.Windows.Input;
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
    private readonly ITrayIconService _trayIconService;
    private readonly IGlobalHotkeyService _globalHotkeys;
    private readonly ICommandPaletteHostService _paletteHost;
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
        vm.ShellActionRequested += OnShellActionRequested;
        vm.CommandPaletteRequested += OnCommandPaletteRequested;

        _trayIconService = App.TrayIconService;
        _trayIconService.Initialize();
        _trayIconService.ActionInvoked += OnTrayActionInvoked;
        _trayIconService.DoubleClicked += OnTrayOpenDoubleClick;

        _globalHotkeys = App.GlobalHotkeyService;
        _globalHotkeys.HotkeyPressed += OnGlobalHotkeyPressed;
        _globalHotkeys.RegistrationsChanged += OnGlobalHotkeyRegistrationsChanged;

        _paletteHost = App.CommandPaletteHostService;
        _paletteHost.CommandInvoked += OnPaletteCommandInvoked;

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
        _trayIconService.UpdateHotkeysEnabled(App.HotkeyService.HotkeysEnabled);

        Loaded += OnLoaded;

        App.NavigationService.NavigateTo("home");
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

        if (vm.IsCommandPaletteOpen && e.Key == Key.Down)
        {
            SelectNextPaletteItem(vm, +1);
            e.Handled = true;
            return;
        }

        if (vm.IsCommandPaletteOpen && e.Key == Key.Up)
        {
            SelectNextPaletteItem(vm, -1);
            e.Handled = true;
            return;
        }
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
            _trayIconService.ShowBalloon("Windows Utility Pack", balloonText, System.Windows.Forms.ToolTipIcon.Info);
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
            NotificationType.Error => System.Windows.Forms.ToolTipIcon.Error,
            NotificationType.Success => System.Windows.Forms.ToolTipIcon.Info,
            _ => System.Windows.Forms.ToolTipIcon.Info,
        };

        _ = Dispatcher.InvokeAsync(() => _trayIconService.ShowBalloon("Windows Utility Pack", e.Message, icon));
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
        var icon = e.State == Models.BackgroundTaskState.Failed ? System.Windows.Forms.ToolTipIcon.Error : System.Windows.Forms.ToolTipIcon.Info;
        _ = Dispatcher.InvokeAsync(() => _trayIconService.ShowBalloon("Background Task", message, icon));
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        App.NotificationService.NotificationRequested -= OnNotificationRequested;
        App.BackgroundTaskService.TaskChanged -= OnBackgroundTaskChanged;
        _trayIconService.ActionInvoked -= OnTrayActionInvoked;
        _trayIconService.DoubleClicked -= OnTrayOpenDoubleClick;
        _globalHotkeys.HotkeyPressed -= OnGlobalHotkeyPressed;
        _globalHotkeys.RegistrationsChanged -= OnGlobalHotkeyRegistrationsChanged;
        _paletteHost.CommandInvoked -= OnPaletteCommandInvoked;
        _paletteHost.Close();

        if (DataContext is MainWindowViewModel vm)
        {
            vm.ShellActionRequested -= OnShellActionRequested;
            vm.CommandPaletteRequested -= OnCommandPaletteRequested;
        }

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void OnTrayOpenDoubleClick(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(RestoreFromTray);
    }

    private void OnGlobalHotkeyPressed(object? sender, ShellHotkeyAction action)
    {
        _ = Dispatcher.InvokeAsync(() => ExecuteShellActionAsync(action));
    }

    private void OnShellActionRequested(object? sender, ShellHotkeyAction action)
    {
        _ = Dispatcher.InvokeAsync(() => ExecuteShellActionAsync(action));
    }

    private void OnCommandPaletteRequested(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() => _paletteHost.ShowOrActivate());
    }

    private void OnTrayActionInvoked(object? sender, TrayMenuAction action)
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            switch (action)
            {
                case TrayMenuAction.OpenMainWindow:
                    RestoreFromTray();
                    break;

                case TrayMenuAction.OpenCommandPalette:
                    await ExecuteShellActionAsync(ShellHotkeyAction.OpenCommandPalette);
                    break;

                case TrayMenuAction.QuickScreenshot:
                    await ExecuteShellActionAsync(ShellHotkeyAction.QuickScreenshot);
                    break;

                case TrayMenuAction.OpenScreenshotAnnotator:
                    await ExecuteShellActionAsync(ShellHotkeyAction.OpenScreenshotAnnotator);
                    break;

                case TrayMenuAction.OpenClipboardManager:
                    RestoreFromTray();
                    App.NavigationService.NavigateTo("clipboard-manager");
                    break;

                case TrayMenuAction.ToggleHotkeys:
                    App.HotkeyService.HotkeysEnabled = !App.HotkeyService.HotkeysEnabled;
                    _trayIconService.UpdateHotkeysEnabled(App.HotkeyService.HotkeysEnabled);
                    break;

                case TrayMenuAction.Exit:
                    _explicitExitRequested = true;
                    Close();
                    break;
            }
        });
    }

    private void OnGlobalHotkeyRegistrationsChanged(object? sender, EventArgs e)
    {
        _trayIconService.UpdateHotkeysEnabled(App.HotkeyService.HotkeysEnabled);
    }

    private void OnPaletteCommandInvoked(object? sender, Models.CommandPaletteItem item)
    {
        _ = Dispatcher.InvokeAsync(() => ExecutePaletteItemAsync(item));
    }

    private async Task ExecuteShellActionAsync(ShellHotkeyAction action)
    {
        var vm = DataContext as MainWindowViewModel;
        if (vm is null)
        {
            return;
        }

        var settings = App.SettingsService.Load();

        switch (action)
        {
            case ShellHotkeyAction.OpenCommandPalette:
                if (settings.RestoreMainWindowOnGlobalAction)
                {
                    RestoreFromTray();
                }

                App.CommandPaletteHostService.ShowOrActivate();
                break;

            case ShellHotkeyAction.QuickScreenshot:
                var result = await App.QuickScreenshotService.CaptureAsync(settings, CancellationToken.None).ConfigureAwait(true);
                if (!result.Success)
                {
                    vm.StatusMessage = result.Message;
                    _trayIconService.ShowBalloon("Quick Screenshot", result.Message, System.Windows.Forms.ToolTipIcon.Error);
                    return;
                }

                App.QuickCaptureStateService.LastCapturePath = result.FilePath;
                vm.StatusMessage = result.Message;
                _trayIconService.ShowBalloon("Quick Screenshot", result.Message, System.Windows.Forms.ToolTipIcon.Info);

                if (settings.QuickScreenshotBehavior == QuickScreenshotBehavior.CaptureToFileAndOpenAnnotator)
                {
                    if (settings.RestoreMainWindowOnGlobalAction)
                    {
                        RestoreFromTray();
                    }
                    App.NavigationService.NavigateTo("screenshot-annotator");
                }
                break;

            case ShellHotkeyAction.OpenScreenshotAnnotator:
                if (settings.RestoreMainWindowOnGlobalAction)
                {
                    RestoreFromTray();
                }
                App.NavigationService.NavigateTo("screenshot-annotator");
                break;

            case ShellHotkeyAction.ToggleMainWindow:
                if (_isHiddenToTray || !IsVisible)
                {
                    RestoreFromTray();
                }
                else
                {
                    HideToTray("Running in system tray.");
                }
                break;

            case ShellHotkeyAction.OpenSettings:
                vm.OpenSettingsCommand.Execute(null);
                break;

            case ShellHotkeyAction.NavigateHome:
                if (settings.RestoreMainWindowOnGlobalAction)
                {
                    RestoreFromTray();
                }
                vm.NavigateHomeCommand.Execute(null);
                break;

            case ShellHotkeyAction.OpenActivityLog:
                if (settings.RestoreMainWindowOnGlobalAction)
                {
                    RestoreFromTray();
                }
                App.NavigationService.NavigateTo("activity-log");
                break;

            case ShellHotkeyAction.OpenTaskMonitor:
                if (settings.RestoreMainWindowOnGlobalAction)
                {
                    RestoreFromTray();
                }
                App.NavigationService.NavigateTo("background-task-monitor");
                break;
        }
    }

    private async Task ExecutePaletteItemAsync(Models.CommandPaletteItem item)
    {
        var vm = DataContext as MainWindowViewModel;
        if (vm is null)
        {
            return;
        }

        if (item.Kind == Models.CommandPaletteItemKind.Tool)
        {
            var settings = App.SettingsService.Load();
            if (settings.RestoreMainWindowOnGlobalAction)
            {
                RestoreFromTray();
            }
            App.NavigationService.NavigateTo(item.CommandKey);
        }
        else
        {
            switch (item.CommandKey)
            {
                case "open-settings":
                    vm.OpenSettingsCommand.Execute(null);
                    break;

                case "home":
                    await ExecuteShellActionAsync(ShellHotkeyAction.NavigateHome);
                    break;

                case "popout-current-tool":
                    vm.PopOutCurrentToolCommand.Execute(null);
                    break;

                case "quick-screenshot":
                    await ExecuteShellActionAsync(ShellHotkeyAction.QuickScreenshot);
                    break;

                case "open-screenshot-annotator":
                    await ExecuteShellActionAsync(ShellHotkeyAction.OpenScreenshotAnnotator);
                    break;

                case "toggle-main-window":
                    await ExecuteShellActionAsync(ShellHotkeyAction.ToggleMainWindow);
                    break;

                case "open-clipboard-manager":
                    RestoreFromTray();
                    App.NavigationService.NavigateTo("clipboard-manager");
                    break;
            }
        }

        App.CommandPaletteService.RecordExecution(item.Id);
        if (App.ActivityLogService is not null)
        {
            await App.ActivityLogService.LogAsync("CommandPalette", "Execute", item.Id).ConfigureAwait(true);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = App.SettingsService.Load();
        if (_trayCoordinator.ShouldStartMinimizedToTray(settings))
        {
            HideToTray("Running in system tray.");
        }
    }

    private void OnTrayExitClick(object? sender, EventArgs e)
    {
        _explicitExitRequested = true;
        _ = Dispatcher.InvokeAsync(Close);
    }
}
