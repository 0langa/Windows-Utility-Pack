using System.IO;
using Microsoft.Win32;

namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// WPF file dialog implementation for QR logo selection and export.
/// </summary>
public sealed class QrCodeFileDialogService : IQrCodeFileDialogService
{
    /// <inheritdoc/>
    public string? PickLogoImagePath()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
            Title = "Select logo image",
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public string? PickExportPath(string suggestedFileName, string initialDirectory, QrCodeExportFormat defaultFormat)
    {
        var extension = GetExtension(defaultFormat);
        var dialog = new SaveFileDialog
        {
            Title = "Export QR code",
            Filter =
                "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp|SVG Vector (*.svg)|*.svg|PDF Document (*.pdf)|*.pdf",
            FileName = Path.GetFileNameWithoutExtension(suggestedFileName),
            DefaultExt = extension,
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public QrCodeExportFormat InferFormat(string filePath, QrCodeExportFormat fallback)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".png") return QrCodeExportFormat.Png;
        if (ext == ".jpg" || ext == ".jpeg") return QrCodeExportFormat.Jpeg;
        if (ext == ".bmp") return QrCodeExportFormat.Bmp;
        if (ext == ".svg") return QrCodeExportFormat.Svg;
        if (ext == ".pdf") return QrCodeExportFormat.Pdf;
        return fallback;
    }

    private static string GetExtension(QrCodeExportFormat format) => format switch
    {
        QrCodeExportFormat.Jpeg => ".jpg",
        QrCodeExportFormat.Bmp => ".bmp",
        QrCodeExportFormat.Svg => ".svg",
        QrCodeExportFormat.Pdf => ".pdf",
        _ => ".png",
    };
}
