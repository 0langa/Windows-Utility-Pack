using WindowsUtilityPack.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

/// <summary>
/// Unit tests for DuplicateGroup model — wasted space calculation
/// and Original file selection logic.
/// </summary>
public class DuplicateGroupTests
{
    private static StorageItem MakeFile(string path, long sizeBytes, DateTime createdAt)
    {
        var item = new StorageItem
        {
            FullPath   = path,
            Name       = System.IO.Path.GetFileName(path),
            SizeBytes  = sizeBytes,
            CreatedAt  = createdAt,
            IsDirectory = false,
        };
        item.TotalSizeBytes = sizeBytes;
        return item;
    }

    [Fact]
    public void WastedBytes_IsFileSizeTimesCountMinusOne()
    {
        var group = new DuplicateGroup
        {
            Files = new System.Collections.Generic.List<StorageItem>
            {
                MakeFile(@"C:.txt", 1000, DateTime.Now.AddDays(-10)),
                MakeFile(@"C:.txt", 1000, DateTime.Now.AddDays(-5)),
                MakeFile(@"C:\c.txt", 1000, DateTime.Now.AddDays(-1)),
            }
        };
        Assert.Equal(2000, group.WastedBytes); // 1000 * (3-1)
    }

    [Fact]
    public void Original_IsOldestFile()
    {
        var oldest = MakeFile(@"C:\old.txt", 1000, DateTime.Now.AddDays(-30));
        var newest = MakeFile(@"C:
ew.txt", 1000, DateTime.Now.AddDays(-1));

        var group = new DuplicateGroup
        {
            Files = new System.Collections.Generic.List<StorageItem> { newest, oldest }
        };

        Assert.Equal(oldest.FullPath, group.Original?.FullPath);
    }

    [Fact]
    public void FileSizeBytes_IsZeroForEmptyGroup()
    {
        var group = new DuplicateGroup { Files = [] };
        Assert.Equal(0L, group.FileSizeBytes);
    }

    [Fact]
    public void WastedBytes_IsZeroForSingleFile()
    {
        var group = new DuplicateGroup
        {
            Files = [MakeFile(@"C:\only.txt", 5000, DateTime.Now)]
        };
        Assert.Equal(0L, group.WastedBytes);
    }

    [Theory]
    [InlineData(1024L,        "1.0 KB")]
    [InlineData(1048576L,     "1.0 MB")]
    [InlineData(1073741824L,  "1.0 GB")]
    public void WastedFormatted_DisplaysCorrectUnit(long fileSize, string expectedUnit)
    {
        var group = new DuplicateGroup
        {
            Files = new System.Collections.Generic.List<StorageItem>
            {
                MakeFile(@"C:\a", fileSize, DateTime.Now.AddDays(-10)),
                MakeFile(@"C:\b", fileSize, DateTime.Now.AddDays(-5)),
            }
        };
        // Wasted = fileSize * 1
        Assert.Contains(expectedUnit.Split(' ')[1], group.WastedFormatted); // e.g. "KB"
    }
}
