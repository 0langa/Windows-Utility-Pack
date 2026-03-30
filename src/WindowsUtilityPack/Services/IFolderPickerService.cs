namespace WindowsUtilityPack.Services;

/// <summary>
/// Abstracts folder-selection dialogs so that ViewModels do not depend directly on
/// <see cref="Microsoft.Win32.OpenFolderDialog"/> (a WPF/Win32 type).
/// </summary>
public interface IFolderPickerService
{
    /// <summary>
    /// Shows a folder-picker dialog and returns the selected path,
    /// or <see langword="null"/> if the user cancelled.
    /// </summary>
    /// <param name="title">Dialog title shown to the user.</param>
    string? PickFolder(string title);
}
