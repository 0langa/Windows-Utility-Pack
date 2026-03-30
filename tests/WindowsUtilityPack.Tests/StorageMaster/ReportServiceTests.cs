using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services.Storage;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

/// <summary>
/// Unit tests for ReportService — CSV and text report generation.
/// </summary>
public class ReportServiceTests
{
    private readonly ReportService _service = new();

    private static StorageItem MakeFile(string name, long sizeBytes)
    {
        var item = new StorageItem
        {
            FullPath     = @"C:\" + name,
            Name         = name,
            IsDirectory  = false,
            SizeBytes    = sizeBytes,
            LastModified = new DateTime(2024, 1, 15, 12, 0, 0),
            CreatedAt    = new DateTime(2023, 6, 1),
        };
        item.TotalSizeBytes = sizeBytes;
        return item;
    }

    [Fact]
    public void ExportFilesToCsv_ContainsHeaderRow()
    {
        var csv = _service.ExportFilesToCsv([MakeFile("test.txt", 1024)]);
        Assert.Contains("Path", csv);
        Assert.Contains("Name", csv);
        Assert.Contains("Size", csv);
    }

    [Fact]
    public void ExportFilesToCsv_ContainsFileData()
    {
        var csv = _service.ExportFilesToCsv([MakeFile("myfile.txt", 2048)]);
        Assert.Contains("myfile.txt", csv);
        Assert.Contains("2048", csv);
    }

    [Fact]
    public void ExportFilesToCsv_HandlesEmptyCollection()
    {
        var csv = _service.ExportFilesToCsv([]);
        // Should have header but no data rows (just the header line)
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void ExportDuplicatesToCsv_ContainsGroupData()
    {
        var f1 = MakeFile("a.mp4", 10485760);
        var f2 = MakeFile("b.mp4", 10485760);
        var group = new DuplicateGroup { GroupKey = "ABC123", Files = [f1, f2] };

        var csv = _service.ExportDuplicatesToCsv([group]);

        Assert.Contains("a.mp4", csv);
        Assert.Contains("b.mp4", csv);
    }

    [Fact]
    public void GenerateSummaryText_ContainsRootPath()
    {
        var root = new StorageItem { FullPath = @"C:\testroot", Name = "testroot", IsDirectory = true };
        root.TotalSizeBytes = 1073741824;
        root.FileCount      = 100;
        root.DirectoryCount = 20;

        var summary = _service.GenerateSummaryText(root);

        Assert.Contains(@"C:\testroot", summary);
        Assert.Contains("SCAN SUMMARY", summary);
    }

    [Fact]
    public void GenerateSummaryText_ContainsFileAndDirCounts()
    {
        var root = new StorageItem { FullPath = @"C:\data", Name = "data", IsDirectory = true };
        root.TotalSizeBytes = 500000;
        root.FileCount      = 42;
        root.DirectoryCount = 7;

        var summary = _service.GenerateSummaryText(root);

        Assert.Contains("42", summary);
        Assert.Contains("7", summary);
    }

    [Fact]
    public void GenerateSummaryText_IncludesDuplicateSummaryWhenProvided()
    {
        var root = new StorageItem { FullPath = @"C:\", Name = "root", IsDirectory = true };
        root.TotalSizeBytes = 1000000;

        var f1 = MakeFile("a.iso", 1073741824);
        var f2 = MakeFile("b.iso", 1073741824);
        var group = new DuplicateGroup { Files = [f1, f2] };

        var summary = _service.GenerateSummaryText(root, [group]);

        Assert.Contains("DUPLICATES", summary);
    }
}
