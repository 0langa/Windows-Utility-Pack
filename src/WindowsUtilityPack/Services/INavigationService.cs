using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Services;

public interface INavigationService
{
    ViewModelBase? CurrentView { get; }
    void Register(string key, Func<ViewModelBase> factory);
    void NavigateTo(string key);
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase, new();
    event EventHandler<ViewModelBase>? Navigated;
}
