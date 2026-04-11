namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// Allows a ViewModel to block navigation while there is unsaved or unsafe state.
/// </summary>
public interface INavigationGuard
{
    /// <summary>
    /// Returns <see langword="true"/> when navigation away from the current ViewModel is allowed.
    /// </summary>
    bool CanNavigateAway();
}
