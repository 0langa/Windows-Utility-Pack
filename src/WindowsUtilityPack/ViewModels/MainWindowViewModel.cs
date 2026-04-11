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
public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private readonly INotificationService? _notifications;
    private AppTheme _effectiveTheme = AppTheme.Dark;
    private string _statusMessage = "Ready";
    private string _notificationText = string.Empty;
    private bool _isNotificationVisible;
    private NotificationType _notificationType;

    public AppTheme EffectiveTheme
    {
        get => _effectiveTheme;
        set
        {
            if (SetProperty(ref _effectiveTheme, value))
            {
                OnPropertyChanged(nameof(ThemeToggleIcon));
                OnPropertyChanged(nameof(ThemeSummary));
            }
        }
    }

    public string ThemeToggleIcon => EffectiveTheme switch
    {
        AppTheme.Light => "\uE706",
        AppTheme.Aurora => "\uEA80",
        _ => "\uE708",
    };

    public string ThemeSummary => _theme.CurrentTheme == AppTheme.System
        ? $"System ({EffectiveTheme})"
        : EffectiveTheme.ToString();

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

        _effectiveTheme = theme.EffectiveTheme;

        Categories = ToolRegistry.GetCategories();

        _navigation.Navigated += OnNavigated;

        _theme.ThemeChanged += OnThemeChanged;

        _notifications = notifications;
        if (_notifications is not null)
        {
            _notifications.NotificationRequested += OnNotificationRequested;
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

    private void OnThemeChanged(object? sender, AppTheme appTheme)
    {
        EffectiveTheme = _theme.EffectiveTheme;
        OnPropertyChanged(nameof(ThemeSummary));
    }

    private void ToggleTheme()
    {
        var newTheme = _theme.CurrentTheme switch
        {
            AppTheme.Dark => AppTheme.Light,
            AppTheme.Light => AppTheme.Aurora,
            AppTheme.Aurora => AppTheme.Dark,
            _ => AppTheme.Dark,
        };

        EffectiveTheme = newTheme;
        _theme.SetTheme(newTheme);
        OnPropertyChanged(nameof(ThemeSummary));

        // Persist immediately so the user doesn't lose their choice on crash.
        var settings = App.SettingsService.Load();
        settings.Theme = newTheme;
        App.SettingsService.Save(settings);
    }

    private static void OpenSettings()
    {
        var window = new SettingsWindow();
        if (Application.Current.MainWindow is { IsLoaded: true } mainWindow)
            window.Owner = mainWindow;
        window.ShowDialog();
    }

    public void Dispose()
    {
        _navigation.Navigated -= OnNavigated;
        _theme.ThemeChanged -= OnThemeChanged;
        if (_notifications is not null)
        {
            _notifications.NotificationRequested -= OnNotificationRequested;
        }
    }
}
