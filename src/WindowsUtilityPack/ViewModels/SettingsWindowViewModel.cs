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

    public RelayCommand SaveCommand { get; }

    public SettingsWindowViewModel(ISettingsService settingsService, IThemeService themeService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

        var settings = _settingsService.Load();
        _selectedTheme = settings.Theme;
        _rememberWindowPosition = settings.RememberWindowPosition;

        SaveCommand = new RelayCommand(_ => SaveSettings());
    }

    private void SaveSettings()
    {
        var settings = _settingsService.Load();
        settings.Theme = _selectedTheme;
        settings.RememberWindowPosition = _rememberWindowPosition;
        _settingsService.Save(settings);
    }
}
