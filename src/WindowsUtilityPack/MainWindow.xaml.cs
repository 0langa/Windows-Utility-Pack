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
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainWindowViewModel(
            App.NavigationService,
            App.ThemeService,
            App.NotificationService,
            App.CommandPaletteService,
            App.ActivityLogService);
        DataContext = vm;

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

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
