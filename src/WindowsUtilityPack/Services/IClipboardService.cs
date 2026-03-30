namespace WindowsUtilityPack.Services;

/// <summary>
/// Abstracts clipboard access so that ViewModels do not depend directly on
/// <see cref="System.Windows.Clipboard"/>.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Attempts to read text from the system clipboard.
    /// Returns <see langword="false"/> when no text is available or clipboard access fails.
    /// </summary>
    bool TryGetText(out string text);

    /// <summary>Places <paramref name="text"/> on the system clipboard.</summary>
    void SetText(string text);
}
