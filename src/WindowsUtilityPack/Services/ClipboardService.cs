using System.Windows;

namespace WindowsUtilityPack.Services;

/// <summary>WPF implementation of <see cref="IClipboardService"/> using <see cref="Clipboard"/>.</summary>
internal sealed class ClipboardService : IClipboardService
{
    /// <inheritdoc/>
    public bool TryGetText(out string text)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                text = Clipboard.GetText();
                return !string.IsNullOrWhiteSpace(text);
            }
        }
        catch
        {
            // Clipboard access can fail when another process owns it temporarily.
        }

        text = string.Empty;
        return false;
    }

    /// <inheritdoc/>
    public void SetText(string text) => Clipboard.SetText(text);
}
