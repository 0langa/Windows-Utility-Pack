namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the home/dashboard view.
/// Extend this to bind feature cards and navigation actions.
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private string _welcomeMessage = "Welcome to Windows Utility Pack";

    public string WelcomeMessage
    {
        get => _welcomeMessage;
        set => SetProperty(ref _welcomeMessage, value);
    }
}
