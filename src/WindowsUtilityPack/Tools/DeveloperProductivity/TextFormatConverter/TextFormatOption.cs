using WindowsUtilityPack.Services.TextConversion;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;

/// <summary>
/// View-friendly representation of a supported text format.
/// </summary>
public sealed class TextFormatOption
{
    public TextFormatOption(TextFormatKind kind)
    {
        Kind = kind;
        DisplayName = kind.ToDisplayName();
        Glyph = kind.GetDisplayGlyph();
    }

    public TextFormatKind Kind { get; }

    public string DisplayName { get; }

    public string Glyph { get; }
}
