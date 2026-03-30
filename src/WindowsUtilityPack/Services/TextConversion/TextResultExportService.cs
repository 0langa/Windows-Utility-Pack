using System.IO;

namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Default save/export flow for text conversion results.
/// </summary>
public sealed class TextResultExportService : ITextResultExportService
{
    private readonly IFileDialogService _fileDialogService;

    public TextResultExportService(IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
    }

    /// <inheritdoc />
    public async Task<string?> SaveAsync(TextConversionResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        var filePath = _fileDialogService.SaveTextFormatFile(result.TargetFormat, result.SuggestedFileName);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        await File.WriteAllBytesAsync(filePath, result.OutputBytes, cancellationToken);
        return filePath;
    }
}
