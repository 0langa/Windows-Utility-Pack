using System.IO;
using WindowsUtilityPack.Services.Storage;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

/// <summary>
/// Unit tests for <see cref="DriveAnalysisService"/>.
/// Tests the folder-size and top-folder methods on real temporary directories.
/// Drive-listing tests are skipped when not running on Windows because
/// <see cref="DriveInfo.GetDrives"/> behaves differently on Linux.
/// </summary>
public class DriveAnalysisServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly DriveAnalysisService _svc = new();

    public DriveAnalysisServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "DriveTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string CreateFile(string relativePath, int sizeBytes)
    {
        var fullPath = Path.Combine(_tempRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, new byte[sizeBytes]);
        return fullPath;
    }

    // ── GetFolderSizeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetFolderSizeAsync_ThrowsArgumentException_WhenPathIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _svc.GetFolderSizeAsync(string.Empty));
    }

    [Fact]
    public async Task GetFolderSizeAsync_ReturnsZero_WhenDirectoryDoesNotExist()
    {
        var missing = Path.Combine(_tempRoot, "missing");

        var size = await _svc.GetFolderSizeAsync(missing);

        Assert.Equal(0L, size);
    }

    [Fact]
    public async Task GetFolderSizeAsync_ReturnsCorrectSize_ForSingleFile()
    {
        CreateFile("file.bin", 1024);

        var size = await _svc.GetFolderSizeAsync(_tempRoot);

        Assert.Equal(1024L, size);
    }

    [Fact]
    public async Task GetFolderSizeAsync_SumsFilesRecursively()
    {
        CreateFile("a.bin", 500);
        CreateFile("sub/b.bin", 300);
        CreateFile("sub/deep/c.bin", 200);

        var size = await _svc.GetFolderSizeAsync(_tempRoot);

        Assert.Equal(1000L, size);
    }

    [Fact]
    public async Task GetFolderSizeAsync_ReturnsZero_WhenDirectoryIsEmpty()
    {
        var size = await _svc.GetFolderSizeAsync(_tempRoot);

        Assert.Equal(0L, size);
    }

    [Fact]
    public async Task GetFolderSizeAsync_SupportsCancellation()
    {
        // Create some files so there is work to cancel.
        for (int i = 0; i < 5; i++)
            CreateFile($"file{i}.bin", 100);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _svc.GetFolderSizeAsync(_tempRoot, cts.Token));
    }

    // ── GetTopFoldersBySize ────────────────────────────────────────────────────

    [Fact]
    public async Task GetTopFoldersBySize_ThrowsArgumentException_WhenRootIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _svc.GetTopFoldersBySize(string.Empty));
    }

    [Fact]
    public async Task GetTopFoldersBySize_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        var missing = Path.Combine(_tempRoot, "missing");

        var result = await _svc.GetTopFoldersBySize(missing);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTopFoldersBySize_ReturnsEmpty_WhenNoSubdirectoriesExist()
    {
        CreateFile("root.bin", 200);

        var result = await _svc.GetTopFoldersBySize(_tempRoot);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTopFoldersBySize_ReturnsFoldersOrderedByDescendingSize()
    {
        CreateFile("small/a.bin", 100);
        CreateFile("large/b.bin", 1000);
        CreateFile("medium/c.bin", 500);

        var result = await _svc.GetTopFoldersBySize(_tempRoot, topN: 10);

        Assert.True(result.Count >= 3);
        // The first entry should be the largest folder.
        Assert.Equal(1000L, result[0].SizeBytes);
        // Verify descending order.
        for (int i = 1; i < result.Count; i++)
            Assert.True(result[i - 1].SizeBytes >= result[i].SizeBytes);
    }

    [Fact]
    public async Task GetTopFoldersBySize_RespectsTopNLimit()
    {
        for (int i = 0; i < 5; i++)
            CreateFile($"folder{i}/data.bin", (i + 1) * 100);

        var result = await _svc.GetTopFoldersBySize(_tempRoot, topN: 3);

        Assert.True(result.Count <= 3);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch { /* cleanup failure should not fail the test */ }
    }
}
