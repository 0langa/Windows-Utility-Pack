using WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;

namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Opens and reuses the modeless preview window for conversion results.
/// </summary>
public interface ITextPreviewWindowService
{
    /// <summary>
    /// True when the preview window is currently open.
    /// </summary>
    bool HasOpenPreview { get; }

    /// <summary>
    /// Shows the preview window or refreshes the existing instance with the latest view model.
    /// </summary>
    void ShowOrUpdate(TextPreviewWindowViewModel viewModel);
}

/// <summary>
/// Minimal preview-window abstraction used to keep the window service testable.
/// </summary>
public interface ITextPreviewWindowHost
{
    object? DataContext { get; set; }

    bool IsVisible { get; }

    event EventHandler? Closed;

    bool Activate();

    void Show();
}

/// <summary>
/// Creates preview window hosts for the conversion preview service.
/// </summary>
public interface ITextPreviewWindowHostFactory
{
    ITextPreviewWindowHost Create(TextPreviewWindowViewModel viewModel);
}
