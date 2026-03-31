using System.IO;
using System.Text;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>Generates CSV and text reports from Storage Master analysis data.</summary>
public class ReportService : IReportService
{
    /// <inheritdoc/>
    public string ExportFilesToCsv(IEnumerable<StorageItem> files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"Path\",\"Name\",\"Extension\",\"Size (Bytes)\",\"Size\",\"Last Modified\",\"Created\",\"Hidden\",\"System\"");

        foreach (var f in files)
        {
            sb.AppendLine(string.Join(",",
                CsvQuote(f.FullPath),
                CsvQuote(f.Name),
                CsvQuote(f.Extension),
                f.SizeBytes,
                CsvQuote(f.DisplaySize),
                CsvQuote(f.LastModified.ToString("yyyy-MM-dd HH:mm:ss")),
                CsvQuote(f.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                f.IsHidden,
                f.IsSystem));
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string ExportDuplicatesToCsv(IEnumerable<DuplicateGroup> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"Group Key\",\"File Count\",\"File Size\",\"Wasted Space\",\"Confidence\",\"File Path\"");

        foreach (var group in groups)
        {
            foreach (var file in group.Files)
            {
                sb.AppendLine(string.Join(",",
                    CsvQuote(group.GroupKey[..Math.Min(16, group.GroupKey.Length)] + "…"),
                    group.Files.Count,
                    CsvQuote(group.FileSizeFormatted),
                    CsvQuote(group.WastedFormatted),
                    CsvQuote(group.Confidence.ToString()),
                    CsvQuote(file.FullPath)));
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string GenerateSummaryText(StorageItem root, IReadOnlyList<DuplicateGroup>? duplicates = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("====================================================");
        sb.AppendLine("  STORAGE MASTER — SCAN SUMMARY REPORT");
        sb.AppendLine("====================================================");
        sb.AppendLine($"  Generated : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  Root Path : {root.FullPath}");
        sb.AppendLine("----------------------------------------------------");
        sb.AppendLine($"  Total Size        : {root.DisplaySize}");
        sb.AppendLine($"  Files Found       : {root.FileCount:N0}");
        sb.AppendLine($"  Directories Found : {root.DirectoryCount:N0}");
        sb.AppendLine();

        // Top 10 largest folders
        var topFolders = root.Children
            .Where(c => c.IsDirectory)
            .OrderByDescending(c => c.TotalSizeBytes)
            .Take(10)
            .ToList();

        if (topFolders.Any())
        {
            sb.AppendLine("  TOP 10 LARGEST FOLDERS:");
            foreach (var folder in topFolders)
                sb.AppendLine($"    {folder.TotalSizeBytes,15:N0} bytes  {folder.Name}");
            sb.AppendLine();
        }

        // Top 10 largest files
        var topFiles = new List<StorageItem>();
        CollectFiles(root, topFiles);
        topFiles.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));

        if (topFiles.Any())
        {
            sb.AppendLine("  TOP 10 LARGEST FILES:");
            foreach (var file in topFiles.Take(10))
                sb.AppendLine($"    {file.SizeBytes,15:N0} bytes  {file.FullPath}");
            sb.AppendLine();
        }

        // Duplicate summary
        if (duplicates != null && duplicates.Any())
        {
            var totalWasted = duplicates.Sum(g => g.WastedBytes);
            sb.AppendLine("  DUPLICATES:");
            sb.AppendLine($"    Groups Found  : {duplicates.Count:N0}");
            sb.AppendLine($"    Total Wasted  : {StorageItem.FormatBytes(totalWasted)}");
            sb.AppendLine();
        }

        sb.AppendLine("====================================================");
        return sb.ToString();
    }

    /// <inheritdoc/>
    public async Task SaveToCsvAsync(string csvContent, string filePath)
        => await File.WriteAllTextAsync(filePath, csvContent, Encoding.UTF8);

    /// <inheritdoc/>
    public async Task SaveToTextAsync(string textContent, string filePath)
        => await File.WriteAllTextAsync(filePath, textContent, Encoding.UTF8);

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string CsvQuote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static void CollectFiles(StorageItem node, List<StorageItem> files)
    {
        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
                CollectFiles(child, files);
            else
                files.Add(child);
        }
    }
}
