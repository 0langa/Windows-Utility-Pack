using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services.Storage;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

/// <summary>
/// Unit tests for CleanupRecommendationService — verifying recommendation
/// categories and risk levels for well-known file patterns.
/// </summary>
public class CleanupRecommendationServiceTests
{
    private readonly CleanupRecommendationService _service = new();

    private static StorageItem MakeTempFile(string name = "file.tmp", long size = 10240)
    {
        var item = new StorageItem
        {
            FullPath    = System.IO.Path.Combine(System.IO.Path.GetTempPath(), name),
            Name        = name,
            IsDirectory = false,
            SizeBytes   = size,
            LastModified = DateTime.Now,
        };
        item.TotalSizeBytes = size;
        return item;
    }

    private static StorageItem MakeLargeStaleFile()
    {
        var item = new StorageItem
        {
            FullPath     = @"C:\data\largefile.iso",
            Name         = "largefile.iso",
            IsDirectory  = false,
            SizeBytes    = 200 * 1024 * 1024, // 200 MB
            LastModified = DateTime.Now.AddDays(-400),
        };
        item.TotalSizeBytes = item.SizeBytes;
        return item;
    }

    private static StorageItem MakeEmptyDir()
    {
        var item = new StorageItem
        {
            FullPath    = @"C:\emptydir",
            Name        = "emptydir",
            IsDirectory = true,
        };
        item.FileCount      = 0;
        item.DirectoryCount = 0;
        item.TotalSizeBytes = 0;
        return item;
    }

    private static StorageItem BuildRoot(params StorageItem[] children)
    {
        var root = new StorageItem { Name = "root", FullPath = @"C:\", IsDirectory = true };
        foreach (var child in children)
        {
            child.Parent = root;
            root.Children.Add(child);
        }
        root.TotalSizeBytes = root.Children.Sum(c => c.TotalSizeBytes);
        root.FileCount      = root.Children.Count(c => !c.IsDirectory);
        root.DirectoryCount = root.Children.Count(c => c.IsDirectory);
        return root;
    }

    [Fact]
    public async Task AnalyseAsync_IdentifiesTempFileInTempFolder()
    {
        var tempFile = MakeTempFile();
        var root     = BuildRoot(tempFile);

        var recs = await _service.AnalyseAsync(root, null, CancellationToken.None);

        Assert.Contains(recs, r => r.Category == CleanupCategory.TemporaryFiles
                                && r.Item.FullPath == tempFile.FullPath);
    }

    [Fact]
    public async Task AnalyseAsync_IdentifiesTmpExtensionFile()
    {
        var tmpFile = new StorageItem
        {
            FullPath = @"C:\work\cache.tmp", Name = "cache.tmp",
            IsDirectory = false, SizeBytes = 1024,
        };
        tmpFile.TotalSizeBytes = tmpFile.SizeBytes;
        var root = BuildRoot(tmpFile);

        var recs = await _service.AnalyseAsync(root, null, CancellationToken.None);

        Assert.Contains(recs, r => r.Category == CleanupCategory.TemporaryFiles
                                && r.Item.FullPath == tmpFile.FullPath);
    }

    [Fact]
    public async Task AnalyseAsync_IdentifiesLargeStaleFile()
    {
        var stale = MakeLargeStaleFile();
        var root  = BuildRoot(stale);

        var recs = await _service.AnalyseAsync(root, null, CancellationToken.None);

        Assert.Contains(recs, r => r.Category == CleanupCategory.LargeStaleFiles
                                && r.Item.FullPath == stale.FullPath);
    }

    [Fact]
    public async Task AnalyseAsync_IdentifiesEmptyDirectory()
    {
        var emptyDir = MakeEmptyDir();
        var root     = BuildRoot(emptyDir);

        var recs = await _service.AnalyseAsync(root, null, CancellationToken.None);

        Assert.Contains(recs, r => r.Category == CleanupCategory.EmptyFolders
                                && r.Item.FullPath == emptyDir.FullPath);
    }

    [Fact]
    public async Task AnalyseAsync_SortsByPotentialSavingsDescending()
    {
        var small = new StorageItem { FullPath = @"C:\work\small.tmp", Name = "small.tmp", IsDirectory = false, SizeBytes = 1024 };
        small.TotalSizeBytes = small.SizeBytes;
        var large = new StorageItem { FullPath = @"C:\work\large.tmp", Name = "large.tmp", IsDirectory = false, SizeBytes = 10485760 };
        large.TotalSizeBytes = large.SizeBytes;
        var root = BuildRoot(small, large);

        var recs = await _service.AnalyseAsync(root, null, CancellationToken.None);

        // Verify descending sort
        for (int i = 1; i < recs.Count; i++)
            Assert.True(recs[i - 1].PotentialSavingsBytes >= recs[i].PotentialSavingsBytes);
    }

    [Fact]
    public async Task AnalyseAsync_IncludesDuplicateRecommendations_WhenProvided()
    {
        var original  = new StorageItem { FullPath = @"C:\orig.mp4",  Name = "orig.mp4",  SizeBytes = 50000, CreatedAt = DateTime.Now.AddDays(-10) };
        var duplicate = new StorageItem { FullPath = @"C:\dupe.mp4",  Name = "dupe.mp4",  SizeBytes = 50000, CreatedAt = DateTime.Now.AddDays(-5) };
        original.TotalSizeBytes  = original.SizeBytes;
        duplicate.TotalSizeBytes = duplicate.SizeBytes;

        var group = new DuplicateGroup { Files = [original, duplicate] };
        var root  = BuildRoot();

        var recs = await _service.AnalyseAsync(root, [group], CancellationToken.None);

        Assert.Contains(recs, r => r.Category == CleanupCategory.DuplicateFiles
                                && r.Item.FullPath == duplicate.FullPath);
        // Original should NOT be recommended for deletion
        Assert.DoesNotContain(recs, r => r.Item.FullPath == original.FullPath
                                      && r.Category == CleanupCategory.DuplicateFiles);
    }

    [Fact]
    public async Task AnalyseAsync_ReturnsEmptyForEmptyRoot()
    {
        var root = BuildRoot();
        var recs = await _service.AnalyseAsync(root, null, CancellationToken.None);
        Assert.Empty(recs);
    }

    [Fact]
    public async Task AnalyseAsync_SupportsCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var root = BuildRoot(MakeTempFile());
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.AnalyseAsync(root, null, cts.Token));
    }
}
