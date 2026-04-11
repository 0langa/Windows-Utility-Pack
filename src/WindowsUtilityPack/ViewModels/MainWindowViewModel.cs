using System.Windows;
using System.Collections.ObjectModel;
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
    private readonly ICommandPaletteService? _commandPalette;
    private readonly IActivityLogService? _activityLogService;
    private readonly IToolWindowHostService? _toolWindowHost;
    private AppTheme _effectiveTheme = AppTheme.Dark;
    private string _statusMessage = "Ready";
    private string _notificationText = string.Empty;
    private bool _isNotificationVisible;
    private NotificationType _notificationType;
    private bool _isCommandPaletteOpen;
    private string _commandPaletteQuery = string.Empty;
    private CommandPaletteItem? _selectedCommandPaletteItem;
    private string _currentToolKey = "home";
    public event EventHandler<ShellHotkeyAction>? ShellActionRequested;

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

    /// <summary>
    /// Indicates whether the command palette overlay is currently open.
    /// </summary>
    public bool IsCommandPaletteOpen
    {
        get => _isCommandPaletteOpen;
        set => SetProperty(ref _isCommandPaletteOpen, value);
    }

    /// <summary>
    /// Current command palette search query.
    /// </summary>
    public string CommandPaletteQuery
    {
        get => _commandPaletteQuery;
        set
        {
            if (SetProperty(ref _commandPaletteQuery, value))
            {
                RefreshCommandPaletteResults();
            }
        }
    }

    /// <summary>
    /// Current selected command palette item.
    /// </summary>
    public CommandPaletteItem? SelectedCommandPaletteItem
    {
        get => _selectedCommandPaletteItem;
        set => SetProperty(ref _selectedCommandPaletteItem, value);
    }

    /// <summary>
    /// In-memory command palette search results.
    /// </summary>
    public ObservableCollection<CommandPaletteItem> CommandPaletteItems { get; } = [];

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
    public RelayCommand PopOutCurrentToolCommand { get; }
    public RelayCommand OpenCommandPaletteCommand { get; }
    public RelayCommand CloseCommandPaletteCommand { get; }
    public AsyncRelayCommand ExecuteCommandPaletteItemCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindowViewModel(INavigationService navigation, IThemeService theme)
        : this(navigation, theme, null) { }

    public MainWindowViewModel(
        INavigationService navigation,
        IThemeService theme,
        INotificationService? notifications,
        ICommandPaletteService? commandPalette = null,
        IActivityLogService? activityLogService = null,
        IToolWindowHostService? toolWindowHost = null)
    {
        _navigation = navigation;
        _theme = theme;
        _commandPalette = commandPalette;
        _activityLogService = activityLogService;
        _toolWindowHost = toolWindowHost;

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
        PopOutCurrentToolCommand = new RelayCommand(_ => PopOutCurrentTool(), _ => _toolWindowHost is not null && !_currentToolKey.Equals("home", StringComparison.OrdinalIgnoreCase));
        OpenCommandPaletteCommand = new RelayCommand(_ => OpenCommandPalette());
        CloseCommandPaletteCommand = new RelayCommand(_ => CloseCommandPalette());
        ExecuteCommandPaletteItemCommand = new AsyncRelayCommand(ExecuteCommandPaletteItemAsync);

        RefreshCommandPaletteResults();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnNavigated(object? sender, Type vmType)
    {
        OnPropertyChanged(nameof(CurrentView));
        _currentToolKey = ResolveToolKey(vmType);
        RelayCommand.RaiseCanExecuteChanged();

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

        if (_activityLogService is not null)
        {
            _ = _activityLogService.LogAsync("Navigation", "Navigate", displayName);
        }
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

    private void OpenCommandPalette()
    {
        IsCommandPaletteOpen = true;
        CommandPaletteQuery = string.Empty;
        RefreshCommandPaletteResults();
    }

    private void CloseCommandPalette()
    {
        IsCommandPaletteOpen = false;
        CommandPaletteQuery = string.Empty;
        SelectedCommandPaletteItem = null;
    }

    private void RefreshCommandPaletteResults()
    {
        if (_commandPalette is null)
        {
            CommandPaletteItems.Clear();
            return;
        }

        var items = _commandPalette.Search(CommandPaletteQuery, limit: 25);
        CommandPaletteItems.Clear();
        foreach (var item in items)
        {
            CommandPaletteItems.Add(item);
        }

        SelectedCommandPaletteItem = CommandPaletteItems.FirstOrDefault();
    }

    private async Task ExecuteCommandPaletteItemAsync(object? parameter)
    {
        var item = parameter as CommandPaletteItem ?? SelectedCommandPaletteItem;
        if (item is null)
        {
            return;
        }

        switch (item.Kind)
        {
            case CommandPaletteItemKind.Tool:
                _navigation.NavigateTo(item.CommandKey);
                break;

            case CommandPaletteItemKind.ShellAction:
                if (item.CommandKey.Equals("open-settings", StringComparison.OrdinalIgnoreCase))
                {
                    OpenSettings();
                }
                else if (item.CommandKey.Equals("home", StringComparison.OrdinalIgnoreCase))
                {
                    _navigation.NavigateTo("home");
                }
                else if (item.CommandKey.Equals("popout-current-tool", StringComparison.OrdinalIgnoreCase))
                {
                    PopOutCurrentTool();
                }
                else if (item.CommandKey.Equals("quick-screenshot", StringComparison.OrdinalIgnoreCase))
                {
                    ShellActionRequested?.Invoke(this, ShellHotkeyAction.QuickScreenshot);
                }
                else if (item.CommandKey.Equals("open-screenshot-annotator", StringComparison.OrdinalIgnoreCase))
                {
                    ShellActionRequested?.Invoke(this, ShellHotkeyAction.OpenScreenshotAnnotator);
                }
                else if (item.CommandKey.Equals("toggle-main-window", StringComparison.OrdinalIgnoreCase))
                {
                    ShellActionRequested?.Invoke(this, ShellHotkeyAction.ToggleMainWindow);
                }
                else if (item.CommandKey.Equals("open-clipboard-manager", StringComparison.OrdinalIgnoreCase))
                {
                    _navigation.NavigateTo("clipboard-manager");
                }
                break;
        }

        if (_activityLogService is not null)
        {
            await _activityLogService
                .LogAsync("CommandPalette", "Execute", item.Id)
                .ConfigureAwait(true);
        }
        _commandPalette?.RecordExecution(item.Id);

        CloseCommandPalette();
    }

    public void OpenCommandPaletteFromShell() => OpenCommandPalette();

    private void PopOutCurrentTool()
    {
        if (_toolWindowHost is null)
        {
            StatusMessage = "Detached tool windows are not available.";
            return;
        }

        if (_currentToolKey.Equals("home", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Home dashboard cannot be opened in a detached window.";
            return;
        }

        var opened = _toolWindowHost.TryOpenOrActivate(_currentToolKey, out var message);
        StatusMessage = message;

        if (opened && _activityLogService is not null)
        {
            _ = _activityLogService.LogAsync("ToolWindows", "OpenOrActivate", _currentToolKey);
        }
    }

    private static string ResolveToolKey(Type vmType)
    {
        var displayName = vmType.Name.Replace("ViewModel", "", StringComparison.OrdinalIgnoreCase);
        foreach (var tool in ToolRegistry.All)
        {
            if (tool.Name.Replace(" ", "", StringComparison.OrdinalIgnoreCase)
                .Equals(displayName, StringComparison.OrdinalIgnoreCase))
            {
                return tool.Key;
            }
        }

        return "home";
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
