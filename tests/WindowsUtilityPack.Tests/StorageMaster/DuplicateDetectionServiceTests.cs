using System.IO;
using WindowsUtilityPack.Services.Storage;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

/// <summary>
/// Unit tests for <see cref="DuplicateDetectionService"/>.
/// Tests use temporary on-disk directories to verify correct detection behaviour
/// and safe traversal on real filesystem structures.
/// </summary>
public class DuplicateDetectionServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly DuplicateDetectionService _svc = new();

    public DuplicateDetectionServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "DupTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string CreateFile(string relativePath, byte[] content)
    {
        var fullPath = Path.Combine(_tempRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        return fullPath;
    }

    private string CreateTextFile(string relativePath, string content)
        => CreateFile(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    // ── Guard tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindDuplicatesAsync_ThrowsArgumentException_WhenRootPathIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _svc.FindDuplicatesAsync(string.Empty));
    }

    [Fact]
    public async Task FindDuplicatesAsync_ThrowsDirectoryNotFoundException_WhenRootDoesNotExist()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _svc.FindDuplicatesAsync(Path.Combine(_tempRoot, "nonexistent")));
    }

    // ── Core detection ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FindDuplicatesAsync_ReturnsEmpty_WhenNoFilesExist()
    {
        var result = await _svc.FindDuplicatesAsync(_tempRoot);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindDuplicatesAsync_ReturnsEmpty_WhenAllFilesAreUnique()
    {
        CreateTextFile("a.txt", "hello");
        CreateTextFile("b.txt", "world");
        CreateTextFile("c.txt", "different content here");

        var result = await _svc.FindDuplicatesAsync(_tempRoot);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindDuplicatesAsync_DetectsDuplicateFiles_SameContent()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };
        CreateFile("original.bin", content);
        CreateFile("copy.bin", content);

        var result = await _svc.FindDuplicatesAsync(_tempRoot);

        Assert.Single(result);
        Assert.Equal(2, result[0].Files.Count);
    }

    [Fact]
    public async Task FindDuplicatesAsync_DoesNotFlagFilesWithSameSizeButDifferentContent()
    {
        // Same size, different content
        CreateFile("a.bin", new byte[] { 0x01, 0x02, 0x03 });
        CreateFile("b.bin", new byte[] { 0xFF, 0xFE, 0xFD });

        var result = await _svc.FindDuplicatesAsync(_tempRoot);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindDuplicatesAsync_FindsDuplicatesInSubdirectories()
    {
        var content = System.Text.Encoding.UTF8.GetBytes("same content");
        CreateFile("root.txt", content);
        CreateFile("subdir/copy.txt", content);

        var result = await _svc.FindDuplicatesAsync(_tempRoot);

        Assert.Single(result);
        Assert.Equal(2, result[0].Files.Count);
    }

    [Fact]
    public async Task FindDuplicatesAsync_ReportsProgress()
    {
        var content = new byte[] { 42, 43, 44 };
        CreateFile("dup1.bin", content);
        CreateFile("dup2.bin", content);

        var progressValues = new List<int>();
        var progress = new Progress<int>(v => progressValues.Add(v));

        await _svc.FindDuplicatesAsync(_tempRoot, progress);
        // Allow progress callbacks to fire
        await Task.Delay(50);

        // Progress should reach 100 for 2 files processed
        Assert.Contains(100, progressValues);
    }

    [Fact]
    public async Task FindDuplicatesAsync_SupportsCancellation()
    {
        // Create several files so the task has work to cancel.
        for (int i = 0; i < 10; i++)
            CreateTextFile($"file{i}.txt", $"content number {i:000}");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _svc.FindDuplicatesAsync(_tempRoot, cancellationToken: cts.Token));
    }

    // ── Safe traversal ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FindDuplicatesAsync_SkipsZeroByteFiles_ForDuplication()
    {
        // Zero-byte files are excluded from hashing (they cannot be duplicates in a meaningful sense).
        CreateFile("empty1.txt", Array.Empty<byte>());
        CreateFile("empty2.txt", Array.Empty<byte>());
        // Also add a real duplicate pair to ensure the service still works.
        var content = new byte[] { 9, 8, 7 };
        CreateFile("real1.bin", content);
        CreateFile("real2.bin", content);

        var result = await _svc.FindDuplicatesAsync(_tempRoot);

        // Only the non-empty duplicate pair should be reported.
        Assert.Single(result);
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
