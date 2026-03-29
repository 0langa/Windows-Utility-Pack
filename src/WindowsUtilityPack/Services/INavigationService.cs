using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Contract for the navigation service used to switch views/pages.
/// Implement this interface to enable multi-page navigation in the future.
/// </summary>
public interface INavigationService
{
    ViewModelBase? CurrentView { get; }
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase, new();
    event EventHandler<ViewModelBase>? Navigated;
}
