using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Services;

public class NavigationService : INavigationService
{
    private readonly Dictionary<string, Func<ViewModelBase>> _factories = new(StringComparer.OrdinalIgnoreCase);
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

    public void Register(string key, Func<ViewModelBase> factory)
        => _factories[key] = factory;

    public void NavigateTo(string key)
    {
        if (_factories.TryGetValue(key, out var factory))
            CurrentView = factory();
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase, new()
        => CurrentView = new TViewModel();
}
