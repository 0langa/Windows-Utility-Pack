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
        private readonly IServiceProvider _serviceProvider;
        private ContentControl? _contentHost;
        private readonly Stack<object> _backStack = new();
        private readonly Dictionary<Type, Func<ViewModelBase>> _factories = new();
        private ViewModelBase _currentViewModel = null!;

        // current view-model exposed as a property (used by MainWindowViewModel)
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            private set
            {
                _currentViewModel = value;
                CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? CurrentViewModelChanged;
        public event EventHandler<Type>? Navigated;
        public bool CanGoBack => _backStack.Count > 0;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
            else
            {
                viewModel = (ViewModelBase)_serviceProvider.GetService(type)!;
            }

            CurrentViewModel = viewModel;
            Navigated?.Invoke(this, type);
        }

        public void NavigateTo(object viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            if (CurrentViewModel is not null)
                _backStack.Push(CurrentViewModel);

            CurrentViewModel = (ViewModelBase)viewModel;

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

        public void Register<TViewModel>(Func<TViewModel> factory) where TViewModel : ViewModelBase
        {
            _factories[typeof(TViewModel)] = factory;
        }
    }
}
