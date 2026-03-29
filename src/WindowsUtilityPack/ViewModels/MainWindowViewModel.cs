using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private bool _isDarkTheme = true;
    private string _statusMessage = "Ready";

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

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ViewModelBase? CurrentView => _navigation.CurrentView;

    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand NavigateHomeCommand { get; }

    public MainWindowViewModel(INavigationService navigation, IThemeService theme)
    {
        _navigation = navigation;
        _theme = theme;
        _isDarkTheme = theme.CurrentTheme == AppTheme.Dark;

        _navigation.Navigated += (_, vm) =>
        {
            OnPropertyChanged(nameof(CurrentView));
            StatusMessage = $"Navigated to {vm.GetType().Name.Replace("ViewModel", "")}";
        };

        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        NavigateCommand = new RelayCommand(key => _navigation.NavigateTo(key?.ToString() ?? "home"));
        NavigateHomeCommand = new RelayCommand(_ => _navigation.NavigateTo("home"));
    }

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _theme.SetTheme(IsDarkTheme ? AppTheme.Dark : AppTheme.Light);
    }
}
