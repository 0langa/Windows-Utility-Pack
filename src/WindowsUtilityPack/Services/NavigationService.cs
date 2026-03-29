using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Default implementation of <see cref="INavigationService"/>.
///
/// Tools are registered once at startup via <see cref="Register"/>.
/// Calling <see cref="NavigateTo(string)"/> creates a fresh ViewModel
/// instance via the factory and raises <see cref="Navigated"/>.
/// The <c>ContentControl</c> in <c>MainWindow.xaml</c> is bound to
/// <see cref="CurrentView"/>; WPF <c>DataTemplate</c> entries in
/// <c>App.xaml</c> then resolve the matching View automatically.
/// </summary>
public class NavigationService : INavigationService
{
    // Keyed registry of ViewModel factories, populated during App startup.
    private readonly Dictionary<string, Func<ViewModelBase>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private ViewModelBase? _currentView;

    /// <inheritdoc/>
    public ViewModelBase? CurrentView
    {
        get => _currentView;
        private set
        {
            _currentView = value;
            // Raise the Navigated event so MainWindowViewModel can update
            // the status bar and other bindings that depend on CurrentView.
            if (value != null)
                Navigated?.Invoke(this, value);
        }
    }

    /// <inheritdoc/>
    public event EventHandler<ViewModelBase>? Navigated;

    /// <inheritdoc/>
    public void Register(string key, Func<ViewModelBase> factory)
        => _factories[key] = factory;

    /// <inheritdoc/>
    public void NavigateTo(string key)
    {
        // Silently ignore unknown keys — prevents crashes from placeholder menu entries.
        if (_factories.TryGetValue(key, out var factory))
            CurrentView = factory();
    }

    /// <inheritdoc/>
    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase, new()
        => CurrentView = new TViewModel();
}
