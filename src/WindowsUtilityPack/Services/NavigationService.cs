using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Simple navigation service that swaps the active ViewModel.
/// Extend this with a DI container for more complex navigation needs.
/// </summary>
public class NavigationService : INavigationService
{
    private ViewModelBase? _currentView;

    public ViewModelBase? CurrentView
    {
        get => _currentView;
        private set
        {
            _currentView = value;
            if (value != null)
                Navigated?.Invoke(this, value);
        }
    }

    public event EventHandler<ViewModelBase>? Navigated;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase, new()
    {
        CurrentView = new TViewModel();
    }
}
