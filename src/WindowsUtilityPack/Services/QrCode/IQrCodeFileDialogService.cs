namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// Abstracts Open/Save file dialogs used by the QR generator.
/// </summary>
public interface IQrCodeFileDialogService
{
    /// <summary>Prompts the user to pick an image file for a logo overlay.</summary>
    string? PickLogoImagePath();

    /// <summary>Prompts the user for an export destination.</summary>
    string? PickExportPath(string suggestedFileName, string initialDirectory, QrCodeExportFormat defaultFormat);

    /// <summary>Infers export format from a selected file path.</summary>
    QrCodeExportFormat InferFormat(string filePath, QrCodeExportFormat fallback);
}
