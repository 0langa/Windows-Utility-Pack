using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the application home/dashboard screen.
/// Provides navigation commands used by the home page feature cards.
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    /// <summary>
    /// Command that accepts a tool key string and navigates to that tool.
    /// Used by the feature cards on the home page (e.g., CommandParameter="disk-info").
    /// </summary>
    public RelayCommand NavigateCommand { get; }

    /// <summary>
    /// Initialises HomeViewModel with a navigation service.
    /// Falls back to the static App.NavigationService if none is provided,
    /// which keeps compatibility with the DataTemplate instantiation path.
    /// </summary>
    public HomeViewModel(INavigationService? navigation = null)
    {
        // Prefer injected service; fall back to static accessor (used when WPF creates the VM via DataTemplate).
        _navigation = navigation ?? App.NavigationService;
        NavigateCommand = new RelayCommand(key => _navigation.NavigateTo(key?.ToString() ?? "home"));
    }
}
