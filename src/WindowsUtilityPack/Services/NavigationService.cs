using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Services
{
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
    public sealed class NavigationService : INavigationService
    {
        private readonly IServiceProvider? _serviceProvider;
        private ContentControl? _contentHost;
        private readonly Stack<object> _backStack = new();
        private readonly Dictionary<Type, Func<ViewModelBase>> _factories = new();
        private readonly Dictionary<string, Func<ViewModelBase>> _keyFactories = new();
        private ViewModelBase _currentViewModel = null!;

        // current view-model exposed as a property (used by MainWindowViewModel)
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            private set
            {
                _currentViewModel = value;
                CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
                CurrentViewChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public object? CurrentView => CurrentViewModel;

        public event EventHandler? CurrentViewModelChanged;
        public event EventHandler? CurrentViewChanged;
        public event EventHandler<Type>? Navigated;
        public bool CanGoBack => _backStack.Count > 0;

        public NavigationService(IServiceProvider? serviceProvider = null)
        {
            _serviceProvider = serviceProvider;
        }

        public void SetContentHost(ContentControl host)
        {
            _contentHost = host ?? throw new ArgumentNullException(nameof(host));
        }

        public void Navigate<TViewModel>() where TViewModel : ViewModelBase
        {
            var type = typeof(TViewModel);
            ViewModelBase viewModel;

            if (_factories.TryGetValue(type, out var factory))
            {
                viewModel = factory();
            }
            else if (_serviceProvider is not null)
            {
                viewModel = (ViewModelBase)_serviceProvider.GetService(type)!;
            }
            else
            {
                throw new InvalidOperationException(
                    $"No factory registered for {type.Name} and no service provider is available.");
            }

            CurrentViewModel = viewModel;
            Navigated?.Invoke(this, type);
        }

        public void NavigateTo(object viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            // If a string key is passed, resolve from registered key-based factories
            if (viewModel is string key)
            {
                if (_keyFactories.TryGetValue(key, out var keyFactory))
                    viewModel = keyFactory();
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NavigationService] NavigateTo: unknown tool key '{key}'. " +
                        "Ensure the tool is registered before navigation is attempted.");
                    return;
                }
            }

            if (CurrentViewModel is not null)
                _backStack.Push(CurrentViewModel);

            if (viewModel is not ViewModelBase vm)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NavigationService] NavigateTo: expected ViewModelBase but received {viewModel.GetType().Name}.");
                return;
            }

            CurrentViewModel = vm;
            Navigated?.Invoke(this, viewModel.GetType());

            if (_contentHost is not null)
                _contentHost.Content = viewModel;
        }

        public void GoBack()
        {
            if (!CanGoBack) return;

            CurrentViewModel = (ViewModelBase)_backStack.Pop();

            if (_contentHost is not null)
                _contentHost.Content = CurrentViewModel;
        }

        public void ClearHistory()
        {
            _backStack.Clear();
        }

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
            => Navigate<TViewModel>();

        public void Register<TViewModel>(Func<TViewModel> factory) where TViewModel : ViewModelBase
        {
            _factories[typeof(TViewModel)] = factory;
        }

        public void Register(string key, Func<ViewModelBase> factory)
        {
            _keyFactories[key] = factory ?? throw new ArgumentNullException(nameof(factory));
        }
    }
}
