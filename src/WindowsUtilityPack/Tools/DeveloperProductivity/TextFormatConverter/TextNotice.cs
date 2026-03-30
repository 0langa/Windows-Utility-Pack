namespace WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;

/// <summary>
/// Severity used by the tool's inline status and warning surfaces.
/// </summary>
public enum TextNoticeSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// Small view model object for inline notices.
/// </summary>
public sealed class TextNotice
{
    public required string Message { get; init; }

    public TextNoticeSeverity Severity { get; init; }
}
