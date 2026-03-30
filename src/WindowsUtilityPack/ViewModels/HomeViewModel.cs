using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the application home/dashboard screen.
/// Provides navigation commands and the tool card data used by the home page.
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
    /// All registered non-home tools from <see cref="ToolRegistry"/>.
    /// Drives the home page feature cards via data binding.
    /// </summary>
    public IReadOnlyList<ToolDefinition> FeaturedTools { get; }

    /// <summary>
    /// Initialises HomeViewModel with the required navigation service.
    /// The service is injected via the factory registered in <c>App.xaml.cs</c>,
    /// ensuring no static fallback is needed.
    /// </summary>
    public HomeViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        NavigateCommand = new RelayCommand(key => _navigation.NavigateTo(key?.ToString() ?? "home"));

        // Expose all registered tools except the "home" entry itself.
        // The home view renders one card per tool using this collection.
        FeaturedTools = ToolRegistry.All
            .Where(t => t.Key != "home")
            .ToList()
            .AsReadOnly();
    }
}
