using Microsoft.Win32;

namespace WindowsUtilityPack.Services;

/// <summary>WPF implementation of <see cref="IFolderPickerService"/> using <see cref="OpenFolderDialog"/>.</summary>
internal sealed class FolderPickerService : IFolderPickerService
{
    /// <inheritdoc/>
    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
