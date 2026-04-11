using System.Windows.Controls;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Abstraction for in-app view-model–based navigation.
/// </summary>
public interface INavigationService
{
    /// <summary>The currently displayed view-model, or null if none.</summary>
    object? CurrentView { get; }

    /// <summary>Raised whenever <see cref="CurrentView"/> changes.</summary>
    event EventHandler? CurrentViewChanged;

    /// <summary>True when the back-stack has at least one entry.</summary>
    bool CanGoBack { get; }

    /// <summary>Attaches the WPF <see cref="ContentControl"/> that hosts views.</summary>
    void SetContentHost(ContentControl host);

    /// <summary>Resolves <typeparamref name="TViewModel"/> from DI and navigates to it.</summary>
    void Navigate<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>Navigates to an already-constructed view-model instance.</summary>
    void NavigateTo(object viewModel);

    /// <summary>Navigates to the previous entry on the back-stack.</summary>
    void GoBack();

    /// <summary>Clears the navigation history without navigating.</summary>
    void ClearHistory();

    /// <summary>
    /// Gets the currently active view model.
    /// </summary>
    ViewModelBase CurrentViewModel { get; }

    /// <summary>
    /// Navigates to the view model of the specified type.
    /// </summary>
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>
    /// Registers a factory for a view model type.
    /// </summary>
    void Register<TViewModel>(Func<TViewModel> factory) where TViewModel : ViewModelBase;

   /// <summary>
   /// Registers a factory for a view model identified by a string key.
   /// </summary>
   void Register(string key, Func<ViewModelBase> factory);

    /// <summary>
    /// Occurs when navigation completes.
    /// </summary>
    event EventHandler<Type>? Navigated;
}
