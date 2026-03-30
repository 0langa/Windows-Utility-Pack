namespace WindowsUtilityPack.Services;

/// <summary>
/// Abstracts clipboard access so that ViewModels do not depend directly on
/// <see cref="System.Windows.Clipboard"/>.
/// </summary>
public interface IClipboardService
{
    /// <summary>Places <paramref name="text"/> on the system clipboard.</summary>
    void SetText(string text);
}
