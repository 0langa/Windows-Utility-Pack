namespace WindowsUtilityPack.Services;

/// <summary>
/// Abstracts user-facing message boxes and confirmation dialogs so that ViewModels
/// do not depend directly on <see cref="System.Windows.MessageBox"/>.
/// </summary>
public interface IUserDialogService
{
    /// <summary>Shows a yes/no confirmation dialog. Returns <see langword="true"/> when the user confirms.</summary>
    bool Confirm(string title, string message);

    /// <summary>Shows an informational message dialog.</summary>
    void ShowInfo(string title, string message);

    /// <summary>Shows an error message dialog.</summary>
    void ShowError(string title, string message);
}
