using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel backing the shell settings dialog.
/// </summary>
public sealed class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private AppTheme _selectedTheme;
    private bool _rememberWindowPosition;
    private bool _trayModeEnabled;
    private bool _minimizeToTray;
    private bool _closeToTray;
    private bool _trayAlertsEnabled;
    private bool _startMinimizedToTray;
    private bool _restoreMainWindowOnGlobalAction;
    private QuickScreenshotBehavior _quickScreenshotBehavior;

    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _themeService.SetTheme(value);
                SaveSettings();
            }
        }
    }

    public bool RememberWindowPosition
    {
        get => _rememberWindowPosition;
        set
        {
            if (SetProperty(ref _rememberWindowPosition, value))
            {
                SaveSettings();
            }
        }
    }

    public bool TrayModeEnabled
    {
        get => _trayModeEnabled;
        set
        {
            if (SetProperty(ref _trayModeEnabled, value))
            {
                SaveSettings();
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value))
            {
                SaveSettings();
            }
        }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (SetProperty(ref _closeToTray, value))
            {
                SaveSettings();
            }
        }
    }

    public bool TrayAlertsEnabled
    {
        get => _trayAlertsEnabled;
        set
        {
            if (SetProperty(ref _trayAlertsEnabled, value))
            {
                SaveSettings();
            }
        }
    }

    public bool StartMinimizedToTray
    {
        get => _startMinimizedToTray;
        set
        {
            if (SetProperty(ref _startMinimizedToTray, value))
            {
                SaveSettings();
            }
        }
    }

    public bool RestoreMainWindowOnGlobalAction
    {
        get => _restoreMainWindowOnGlobalAction;
        set
        {
            if (SetProperty(ref _restoreMainWindowOnGlobalAction, value))
            {
                SaveSettings();
            }
        }
    }

    public QuickScreenshotBehavior QuickScreenshotBehavior
    {
        get => _quickScreenshotBehavior;
        set
        {
            if (SetProperty(ref _quickScreenshotBehavior, value))
            {
                SaveSettings();
            }
        }
    }

    public IReadOnlyList<QuickScreenshotBehavior> QuickScreenshotBehaviorOptions { get; } =
        Enum.GetValues<QuickScreenshotBehavior>();

    public RelayCommand SaveCommand { get; }

    public SettingsWindowViewModel(ISettingsService settingsService, IThemeService themeService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

        var settings = _settingsService.Load();
        _selectedTheme = settings.Theme;
        _rememberWindowPosition = settings.RememberWindowPosition;
        _trayModeEnabled = settings.TrayModeEnabled;
        _minimizeToTray = settings.MinimizeToTray;
        _closeToTray = settings.CloseToTray;
        _trayAlertsEnabled = settings.TrayAlertsEnabled;
        _startMinimizedToTray = settings.StartMinimizedToTray;
        _restoreMainWindowOnGlobalAction = settings.RestoreMainWindowOnGlobalAction;
        _quickScreenshotBehavior = settings.QuickScreenshotBehavior;

        SaveCommand = new RelayCommand(_ => SaveSettings());
    }

    private void SaveSettings()
    {
        var settings = _settingsService.Load();
        settings.Theme = _selectedTheme;
        settings.RememberWindowPosition = _rememberWindowPosition;
        settings.TrayModeEnabled = _trayModeEnabled;
        settings.MinimizeToTray = _minimizeToTray;
        settings.CloseToTray = _closeToTray;
        settings.TrayAlertsEnabled = _trayAlertsEnabled;
        settings.StartMinimizedToTray = _startMinimizedToTray;
        settings.RestoreMainWindowOnGlobalAction = _restoreMainWindowOnGlobalAction;
        settings.QuickScreenshotBehavior = _quickScreenshotBehavior;
        _settingsService.Save(settings);
    }
}
