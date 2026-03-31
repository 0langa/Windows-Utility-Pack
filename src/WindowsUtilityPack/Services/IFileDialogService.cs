using WindowsUtilityPack.Services.TextConversion;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Abstracts file open/save dialogs so ViewModels remain independent from
/// WPF-specific dialog types.
/// </summary>
public interface IFileDialogService
{
	/// <summary>
	/// Opens a file picker configured for the supported text conversion formats.
	/// Returns the selected file path, or <see langword="null"/> when cancelled.
	/// </summary>
	string? OpenTextFormatFile();

	/// <summary>
	/// Opens a save dialog for the given target <paramref name="format"/> and
	/// returns the selected file path, or <see langword="null"/> when cancelled.
	/// </summary>
	string? SaveTextFormatFile(TextFormatKind format, string suggestedFileName);
}
