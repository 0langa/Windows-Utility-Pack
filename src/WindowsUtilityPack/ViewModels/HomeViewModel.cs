using WindowsUtilityPack.Commands;

namespace WindowsUtilityPack.ViewModels;

public class HomeViewModel : ViewModelBase
{
    public RelayCommand NavigateCommand { get; }

    public HomeViewModel()
    {
        NavigateCommand = new RelayCommand(key => App.NavigationService.NavigateTo(key?.ToString() ?? "home"));
    }
}
