using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services.Storage;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

/// <summary>
/// Unit tests for SnapshotService — snapshot creation, comparison logic.
/// Uses an in-memory approach by testing the Compare method directly.
/// </summary>
public class SnapshotServiceTests
{
    private readonly SnapshotService _service = new();

    private static StorageSnapshot MakeSnapshot(string rootPath, long totalBytes, int fileCount, params (string path, string name, long size)[] folders)
    {
        return new StorageSnapshot
        {
            RootPath       = rootPath,
            TotalSizeBytes = totalBytes,
            FileCount      = fileCount,
            TakenAt        = DateTime.Now,
            TopFolders     = folders.Select(f => new SnapshotFolderEntry
            {
                Path = f.path, Name = f.name, SizeBytes = f.size
            }).ToList(),
        };
    }

    [Fact]
    public void Compare_SizeDelta_IsCorrect()
    {
        var baseline = MakeSnapshot(@"C:\", 1_000_000_000L, 100);
        var current  = MakeSnapshot(@"C:\", 1_500_000_000L, 150);

        var cmp = _service.Compare(baseline, current);

        Assert.Equal(500_000_000L, cmp.SizeDeltaBytes);
        Assert.Equal(50, cmp.FileDeltaCount);
    }

    [Fact]
    public void Compare_SizeDelta_CanBeNegative()
    {
        var baseline = MakeSnapshot(@"C:\", 2_000_000_000L, 200);
        var current  = MakeSnapshot(@"C:\", 1_500_000_000L, 150);

        var cmp = _service.Compare(baseline, current);

        Assert.Equal(-500_000_000L, cmp.SizeDeltaBytes);
        Assert.Equal(-50, cmp.FileDeltaCount);
    }

    [Fact]
    public void Compare_FolderGrowth_DetectsGrowingFolder()
    {
        var baseline = MakeSnapshot(@"C:\", 1_000_000_000L, 100,
            (@"C:\Videos", "Videos", 500_000_000L));
        var current  = MakeSnapshot(@"C:\", 2_000_000_000L, 200,
            (@"C:\Videos", "Videos", 1_500_000_000L));

        var cmp = _service.Compare(baseline, current);

        var videoEntry = cmp.FolderGrowth.FirstOrDefault(e => e.FolderName == "Videos");
        Assert.NotNull(videoEntry);
        Assert.Equal(1_000_000_000L, videoEntry.DeltaBytes);
    }

    [Fact]
    public void Compare_FolderGrowth_IncludesFolderOnlyInBaseline()
    {
        var baseline = MakeSnapshot(@"C:\", 2_000_000_000L, 200,
            (@"C:\OldFolder", "OldFolder", 500_000_000L));
        var current  = MakeSnapshot(@"C:\", 1_500_000_000L, 150);

        var cmp = _service.Compare(baseline, current);

        var oldEntry = cmp.FolderGrowth.FirstOrDefault(e => e.FolderName == "OldFolder");
        Assert.NotNull(oldEntry);
        Assert.Equal(-500_000_000L, oldEntry.DeltaBytes); // Folder was deleted or shrunk to 0
    }

    [Fact]
    public void Compare_FolderGrowth_SortedByAbsoluteDeltaDescending()
    {
        var baseline = MakeSnapshot(@"C:\", 1_000_000_000L, 100,
            (@"C:\A", "A", 100_000_000L),
            (@"C:\B", "B", 100_000_000L));
        var current  = MakeSnapshot(@"C:\", 2_000_000_000L, 200,
            (@"C:\A", "A", 200_000_000L),  // +100 MB
            (@"C:\B", "B", 600_000_000L)); // +500 MB (larger delta)

        var cmp = _service.Compare(baseline, current);

        // B should appear before A (larger delta)
        var aIdx = cmp.FolderGrowth.FindIndex(e => e.FolderName == "A");
        var bIdx = cmp.FolderGrowth.FindIndex(e => e.FolderName == "B");
        Assert.True(bIdx < aIdx);
    }

    [Fact]
    public void StorageSnapshot_DisplayLabel_UsesLabelWhenSet()
    {
        var snap = new StorageSnapshot { Label = "My Snapshot", TakenAt = DateTime.Now, TotalSizeBytes = 1024 };
        Assert.Equal("My Snapshot", snap.DisplayLabel);
    }

    [Fact]
    public void StorageSnapshot_DisplayLabel_FallsBackToAutoLabel()
    {
        var snap = new StorageSnapshot { Label = "", TakenAt = new DateTime(2024, 6, 15, 14, 30, 0), TotalSizeBytes = 1073741824L };
        Assert.Contains("2024-06-15", snap.DisplayLabel);
        Assert.Contains("1.0 GB", snap.DisplayLabel);
    }
}
