using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Manages in-app navigation between tool ViewModels.
///
/// The navigation model is intentionally ViewModel-first:
/// each tool is identified by a string key (e.g. <c>"disk-info"</c>).
/// When a key is navigated to, a fresh ViewModel instance is created
/// via its registered factory.  WPF <c>DataTemplate</c> entries in
/// <c>App.xaml</c> then automatically resolve the correct View.
/// </summary>
public interface INavigationService
{
    /// <summary>Gets the currently displayed ViewModel, or <see langword="null"/> on startup.</summary>
    ViewModelBase? CurrentView { get; }

    /// <summary>Gets the key of the currently displayed tool, or <see langword="null"/> before first navigation.</summary>
    string? CurrentKey { get; }

    /// <summary>
    /// Registers a named factory so that <see cref="NavigateTo(string)"/> can create the ViewModel.
    /// Called once during application startup for each tool.
    /// </summary>
    void Register(string key, Func<ViewModelBase> factory);

    /// <summary>Navigates to the tool registered under <paramref name="key"/>.</summary>
    /// <param name="key">The tool key (case-insensitive).  Unknown keys are silently ignored.</param>
    void NavigateTo(string key);

    /// <summary>
    /// Navigates directly to a new instance of <typeparamref name="TViewModel"/>,
    /// bypassing the key registry.  Useful for programmatic navigation in tests.
    /// </summary>
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase, new();

    /// <summary>Raised after every successful navigation with the new ViewModel as the argument.</summary>
    event EventHandler<ViewModelBase>? Navigated;
}
