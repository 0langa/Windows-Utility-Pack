using WindowsUtilityPack.Commands;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Handles top-level navigation and theme toggling.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private bool _isDarkTheme = true;

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
                OnPropertyChanged(nameof(ThemeToggleIcon));
        }
    }

    public string ThemeToggleIcon => IsDarkTheme ? "☀" : "🌙";

    public RelayCommand ToggleThemeCommand { get; }

    public MainWindowViewModel()
    {
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
    }

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        App.ThemeService.SetTheme(IsDarkTheme ? AppTheme.Dark : AppTheme.Light);
    }
}
