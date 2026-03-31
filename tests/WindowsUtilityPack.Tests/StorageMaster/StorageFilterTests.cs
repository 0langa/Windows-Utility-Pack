using WindowsUtilityPack.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

/// <summary>
/// Unit tests for the StorageFilter model — verifying that filter predicates
/// correctly include/exclude StorageItem instances.
/// </summary>
public class StorageFilterTests
{
    private static StorageItem MakeFile(
        string name          = "file.txt",
        long   sizeBytes     = 1024,
        bool   isHidden      = false,
        bool   isSystem      = false,
        int    ageDays       = 10,
        bool   isDirectory   = false)
    {
        var attrs = System.IO.FileAttributes.Normal;
        if (isHidden) attrs |= System.IO.FileAttributes.Hidden;
        if (isSystem) attrs |= System.IO.FileAttributes.System;

        var item = new StorageItem
        {
            Name         = name,
            FullPath     = @"C:	est" + name,
            IsDirectory  = isDirectory,
            SizeBytes    = sizeBytes,
            Attributes   = attrs,
            LastModified = DateTime.Now.AddDays(-ageDays),
        };
        item.TotalSizeBytes = sizeBytes;
        return item;
    }

    [Fact]
    public void DefaultFilter_MatchesRegularFile()
    {
        var filter = new StorageFilter { ShowFiles = true };
        var item   = MakeFile();
        Assert.True(filter.Matches(item));
    }

    [Fact]
    public void ShowFiles_False_ExcludesFiles()
    {
        var filter = new StorageFilter { ShowFiles = false, ShowDirectories = true };
        var item   = MakeFile();
        Assert.False(filter.Matches(item));
    }

    [Fact]
    public void ShowDirectories_False_ExcludesDirectories()
    {
        var filter = new StorageFilter { ShowFiles = true, ShowDirectories = false };
        var item   = MakeFile(isDirectory: true);
        Assert.False(filter.Matches(item));
    }

    [Fact]
    public void ShowHidden_False_ExcludesHiddenFiles()
    {
        var filter = new StorageFilter { ShowFiles = true, ShowHidden = false };
        var item   = MakeFile(isHidden: true);
        Assert.False(filter.Matches(item));
    }

    [Fact]
    public void ShowHidden_True_IncludesHiddenFiles()
    {
        var filter = new StorageFilter { ShowFiles = true, ShowHidden = true };
        var item   = MakeFile(isHidden: true);
        Assert.True(filter.Matches(item));
    }

    [Fact]
    public void ShowSystem_False_ExcludesSystemFiles()
    {
        var filter = new StorageFilter { ShowFiles = true, ShowSystem = false };
        var item   = MakeFile(isSystem: true);
        Assert.False(filter.Matches(item));
    }

    [Fact]
    public void MinSizeBytes_ExcludesSmallFiles()
    {
        var filter = new StorageFilter { ShowFiles = true, MinSizeBytes = 10000 };
        var item   = MakeFile(sizeBytes: 500);
        Assert.False(filter.Matches(item));
    }

    [Fact]
    public void MinSizeBytes_IncludesLargeFiles()
    {
        var filter = new StorageFilter { ShowFiles = true, MinSizeBytes = 100 };
        var item   = MakeFile(sizeBytes: 50000);
        Assert.True(filter.Matches(item));
    }

    [Fact]
    public void MaxSizeBytes_ExcludesLargeFiles()
    {
        var filter = new StorageFilter { ShowFiles = true, MaxSizeBytes = 100 };
        var item   = MakeFile(sizeBytes: 50000);
        Assert.False(filter.Matches(item));
    }

    [Fact]
    public void ExtensionFilter_ExcludesWrongExtension()
    {
        var filter = new StorageFilter { ShowFiles = true, ExtensionFilter = ".mp4" };
        var item   = MakeFile(name: "file.txt");
        Assert.False(filter.Matches(item));
    }

    [Fact]
    public void ExtensionFilter_IncludesMatchingExtension()
    {
        var filter = new StorageFilter { ShowFiles = true, ExtensionFilter = ".txt" };
        var item   = MakeFile(name: "file.txt");
        Assert.True(filter.Matches(item));
    }

    [Fact]
    public void SearchText_ExcludesNonMatchingPaths()
    {
        var filter = new StorageFilter { ShowFiles = true, SearchText = "uniquepattern" };
        var item   = MakeFile(name: "other.txt");
        Assert.False(filter.Matches(item));
    }

    [Fact]
    public void SearchText_IncludesMatchingPaths()
    {
        var filter = new StorageFilter { ShowFiles = true, SearchText = "test" };
        var item   = MakeFile(name: "testfile.txt");
        Assert.True(filter.Matches(item));
    }

    [Fact]
    public void OlderThanDays_ExcludesRecentFiles()
    {
        var filter = new StorageFilter { ShowFiles = true, OlderThanDays = 365 };
        var item   = MakeFile(ageDays: 10);
        Assert.False(filter.Matches(item));
    }

    [Fact]
    public void OlderThanDays_IncludesOldFiles()
    {
        var filter = new StorageFilter { ShowFiles = true, OlderThanDays = 30 };
        var item   = MakeFile(ageDays: 400);
        Assert.True(filter.Matches(item));
    }
}
