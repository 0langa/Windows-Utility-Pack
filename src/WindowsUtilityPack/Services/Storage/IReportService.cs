using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>Formats and exports storage analysis results.</summary>
public interface IReportService
{
    /// <summary>Generates a CSV export of the flat file list.</summary>
    string ExportFilesToCsv(IEnumerable<StorageItem> files);

    /// <summary>Generates a CSV export of duplicate groups.</summary>
    string ExportDuplicatesToCsv(IEnumerable<DuplicateGroup> groups);

    /// <summary>Generates a plain-text summary report for a scan.</summary>
    string GenerateSummaryText(StorageItem root, IReadOnlyList<DuplicateGroup>? duplicates = null);

    /// <summary>Writes the CSV content to a file at the given path.</summary>
    Task SaveToCsvAsync(string csvContent, string filePath);

    /// <summary>Writes the text content to a file at the given path.</summary>
    Task SaveToTextAsync(string textContent, string filePath);
}
