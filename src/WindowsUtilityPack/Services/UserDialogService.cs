using System.Windows;

namespace WindowsUtilityPack.Services;

/// <summary>WPF implementation of <see cref="IUserDialogService"/> using <see cref="MessageBox"/>.</summary>
internal sealed class UserDialogService : IUserDialogService
{
    /// <inheritdoc/>
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <inheritdoc/>
    public void ShowInfo(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    /// <inheritdoc/>
    public void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
