using System.IO;
using WindowsUtilityPack.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

/// <summary>
/// Unit tests for StorageItem model behaviour:
/// formatting, computed properties, and tree aggregation expectations.
/// </summary>
public class StorageItemTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1048576L, "1.0 MB")]
    [InlineData(1073741824L, "1.0 GB")]
    [InlineData(1099511627776L, "1.0 TB")]
    [InlineData(1536L, "1.5 KB")]
    public void FormatBytes_ReturnsCorrectString(long bytes, string expected)
    {
        Assert.Equal(expected, StorageItem.FormatBytes(bytes));
    }

    [Fact]
    public void IsHidden_ReturnsTrueWhenHiddenAttributeSet()
    {
        var item = new StorageItem { Attributes = FileAttributes.Hidden };
        Assert.True(item.IsHidden);
    }

    [Fact]
    public void IsSystem_ReturnsTrueWhenSystemAttributeSet()
    {
        var item = new StorageItem { Attributes = FileAttributes.System };
        Assert.True(item.IsSystem);
    }

    [Fact]
    public void IsReadOnly_ReturnsTrueWhenReadOnlyAttributeSet()
    {
        var item = new StorageItem { Attributes = FileAttributes.ReadOnly };
        Assert.True(item.IsReadOnly);
    }

    [Fact]
    public void Extension_IsEmptyForDirectories()
    {
        var dir = new StorageItem { Name = "TestFolder", IsDirectory = true };
        Assert.Equal(string.Empty, dir.Extension);
    }

    [Theory]
    [InlineData("file.txt", ".txt")]
    [InlineData("VIDEO.MP4", ".mp4")]
    [InlineData("archive.tar.gz", ".gz")]
    [InlineData("noextension", "")]
    public void Extension_ReturnsLowerCaseExtension(string name, string expectedExt)
    {
        var item = new StorageItem { Name = name, IsDirectory = false };
        Assert.Equal(expectedExt, item.Extension);
    }

    [Fact]
    public void DisplaySize_UsesTotalSizeBytesForDirectory()
    {
        var dir = new StorageItem
        {
            IsDirectory    = true,
            TotalSizeBytes = 1073741824L, // 1 GB
        };
        Assert.Equal("1.0 GB", dir.DisplaySize);
    }

    [Fact]
    public void DisplaySize_UsesSizeBytesForFile()
    {
        var file = new StorageItem
        {
            IsDirectory = false,
            SizeBytes   = 1048576L, // 1 MB
        };
        Assert.Equal("1.0 MB", file.DisplaySize);
    }

    [Fact]
    public void AgeDays_ReturnsCorrectDayCount()
    {
        var item = new StorageItem { LastModified = DateTime.Now.AddDays(-100) };
        // Allow 1 day margin for test timing
        Assert.InRange(item.AgeDays, 99, 101);
    }

    [Fact]
    public void IsStale_ReturnsTrueWhenOlderThan365Days()
    {
        var item = new StorageItem { LastModified = DateTime.Now.AddDays(-400) };
        Assert.True(item.IsStale);
    }

    [Fact]
    public void IsStale_ReturnsFalseForRecentFile()
    {
        var item = new StorageItem { LastModified = DateTime.Now.AddDays(-30) };
        Assert.False(item.IsStale);
    }

    [Fact]
    public void Children_IsEmptyByDefault()
    {
        var item = new StorageItem { IsDirectory = true };
        Assert.Empty(item.Children);
    }

    [Fact]
    public void ToString_ContainsDirFlagAndPath()
    {
        var dir = new StorageItem { FullPath = @"C:	est", Name = "test", IsDirectory = true };
        dir.TotalSizeBytes = 1024;
        Assert.Contains("DIR", dir.ToString());
        Assert.Contains(@"C:	est", dir.ToString());
    }
}
