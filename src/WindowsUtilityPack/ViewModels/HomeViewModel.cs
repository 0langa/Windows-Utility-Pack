using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the application home/dashboard screen.
/// Provides navigation commands and exposes tool definitions from
/// <see cref="ToolRegistry"/> so home cards are generated dynamically.
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    /// <summary>
    /// Command that accepts a tool key string and navigates to that tool.
    /// </summary>
    public RelayCommand NavigateCommand { get; }

    /// <summary>
    /// Tool definitions displayed as feature cards on the home page.
    /// Excludes the "General" category (home itself).
    /// </summary>
    public IReadOnlyList<ToolDefinition> DisplayTools { get; }

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
        DisplayTools = ToolRegistry.GetDisplayTools();
    }
}
