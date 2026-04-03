using System.Windows;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.Views;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the application shell.
/// Exposes category navigation data from <see cref="ToolRegistry"/> so the
/// shell menus are driven by a single source of truth.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private bool _isDarkTheme = true;
    private string _statusMessage = "Ready";
    private string _notificationText = string.Empty;
    private bool _isNotificationVisible;
    private NotificationType _notificationType;

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
                OnPropertyChanged(nameof(ThemeToggleIcon));
        }
    }

    /// <summary>
    /// Segoe MDL2 Assets glyph: sun when dark (switch to light), moon when light (switch to dark).
    /// </summary>
    public string ThemeToggleIcon => IsDarkTheme ? "\uE706" : "\uE793";

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Text displayed in the in-app notification banner.</summary>
    public string NotificationText
    {
        get => _notificationText;
        set => SetProperty(ref _notificationText, value);
    }

    /// <summary>Controls visibility of the notification banner.</summary>
    public bool IsNotificationVisible
    {
        get => _isNotificationVisible;
        set => SetProperty(ref _isNotificationVisible, value);
    }

    /// <summary>Severity of the current notification (Info, Success, Error).</summary>
    public NotificationType NotificationType
    {
        get => _notificationType;
        set => SetProperty(ref _notificationType, value);
    }

    public ViewModelBase? CurrentView => _navigation.CurrentView as ViewModelBase;

    /// <summary>
    /// Navigation categories built from <see cref="ToolRegistry"/> metadata.
    /// Used by the nav bar to generate category buttons dynamically.
    /// </summary>
    public IReadOnlyList<CategoryItem> Categories { get; }

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand NavigateHomeCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand DismissNotificationCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindowViewModel(INavigationService navigation, IThemeService theme)
        : this(navigation, theme, null) { }

    public MainWindowViewModel(
        INavigationService navigation,
        IThemeService theme,
        INotificationService? notifications)
    {
        _navigation = navigation;
        _theme = theme;

        _isDarkTheme = theme.EffectiveTheme == AppTheme.Dark;

        Categories = ToolRegistry.GetCategories();

        _navigation.Navigated += OnNavigated;

        _theme.ThemeChanged += (_, _) =>
        {
            IsDarkTheme = _theme.EffectiveTheme == AppTheme.Dark;
        };

        if (notifications is not null)
        {
            notifications.NotificationRequested += OnNotificationRequested;
        }

        ToggleThemeCommand  = new RelayCommand(_ => ToggleTheme());
        NavigateCommand     = new RelayCommand(key => _navigation.NavigateTo(key?.ToString() ?? "home"));
        NavigateHomeCommand = new RelayCommand(_ => _navigation.NavigateTo("home"));
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        DismissNotificationCommand = new RelayCommand(_ => IsNotificationVisible = false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnNavigated(object? sender, Type vmType)
    {
        OnPropertyChanged(nameof(CurrentView));

        // Use the registered display name from ToolRegistry instead of the class name.
        var displayName = vmType.Name.Replace("ViewModel", "");
        foreach (var tool in ToolRegistry.All)
        {
            // Compare tool name (spaces removed) to the VM type name (without "ViewModel").
            if (tool.Name.Replace(" ", "").Equals(displayName, StringComparison.OrdinalIgnoreCase))
            {
                displayName = tool.Name;
                break;
            }
        }

        StatusMessage = $"Navigated to {displayName}";
    }

    private void OnNotificationRequested(object? sender, NotificationEventArgs e)
    {
        NotificationText = e.Message;
        NotificationType = e.Type;
        IsNotificationVisible = true;
    }

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        var newTheme = IsDarkTheme ? AppTheme.Dark : AppTheme.Light;
        _theme.SetTheme(newTheme);

        // Persist immediately so the user doesn't lose their choice on crash.
        var settings = App.SettingsService.Load();
        settings.Theme = newTheme;
        App.SettingsService.Save(settings);
    }

    private static void OpenSettings()
    {
        var window = new SettingsWindow
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
    }
}
