namespace WindowsUtilityPack.Services;

using Microsoft.Win32;
using WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// WPF implementation of <see cref="IFileDialogService"/> using the standard
/// Windows open/save file dialogs.
/// </summary>
internal sealed class FileDialogService : IFileDialogService
{
    /// <inheritdoc/>
    public string? OpenTextFormatFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open a text or document file",
            Filter = TextFormatKindExtensions.BuildOpenFileFilter(),
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public string? SaveTextFormatFile(TextFormatKind format, string suggestedFileName)
    {
        var safeName = string.IsNullOrWhiteSpace(suggestedFileName)
            ? $"converted{format.GetDefaultExtension()}"
            : suggestedFileName;

        var dialog = new SaveFileDialog
        {
            Title = $"Save as {format.ToDisplayName()}",
            FileName = safeName,
            DefaultExt = format.GetDefaultExtension(),
            Filter = format.BuildSaveFilter(),
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}