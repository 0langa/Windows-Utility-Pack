using System.Windows;

namespace WindowsUtilityPack.Services;

/// <summary>WPF implementation of <see cref="IClipboardService"/> using <see cref="Clipboard"/>.</summary>
internal sealed class ClipboardService : IClipboardService
{
    /// <inheritdoc/>
    public void SetText(string text) => Clipboard.SetText(text);
}
