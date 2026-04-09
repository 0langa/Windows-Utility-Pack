using System.IO;
using System.Text.Json;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>Tests for DownloadHistoryService covering atomic write and bounded size.</summary>
public class DownloadHistoryServiceTests : IDisposable
{
    // Point the service at a temp file so tests don't touch the real history.
    private readonly string _tempDir;

    public DownloadHistoryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task AppendAsync_Persists_Entry_To_File()
    {
        var service = new DownloadHistoryService(_tempDir);
        var entry = MakeEntry("https://example.com/a.zip");

        await service.AppendAsync(entry);

        var loaded = await service.LoadAsync();
        var single = Assert.Single(loaded);
        Assert.Equal(entry.SourceUrl, single.SourceUrl);
    }

    [Fact]
    public async Task AppendAsync_Newest_Entry_First()
    {
        var service = new DownloadHistoryService(_tempDir);

        await service.AppendAsync(MakeEntry("https://example.com/first.zip"));
        await service.AppendAsync(MakeEntry("https://example.com/second.zip"));

        var loaded = await service.LoadAsync();
        Assert.Equal(2, loaded.Count);
        Assert.Equal("https://example.com/second.zip", loaded[0].SourceUrl);
        Assert.Equal("https://example.com/first.zip", loaded[1].SourceUrl);
    }

    [Fact]
    public async Task AppendAsync_Caps_At_1000_Entries()
    {
        var service = new DownloadHistoryService(_tempDir);

        // Write 1002 entries — oldest two should be dropped
        for (var i = 0; i < 1002; i++)
        {
            await service.AppendAsync(MakeEntry($"https://example.com/{i}.zip"));
        }

        var loaded = await service.LoadAsync();
        Assert.Equal(1000, loaded.Count);
        // Most recent is index 1001
        Assert.Equal("https://example.com/1001.zip", loaded[0].SourceUrl);
    }

    [Fact]
    public async Task AppendAsync_Leaves_No_Tmp_File_After_Write()
    {
        var service = new DownloadHistoryService(_tempDir);
        await service.AppendAsync(MakeEntry("https://example.com/check.zip"));

        var tmpFiles = Directory.GetFiles(_tempDir, "*.tmp");
        Assert.Empty(tmpFiles);
    }

    [Fact]
    public async Task ClearAsync_Removes_All_Entries()
    {
        var service = new DownloadHistoryService(_tempDir);
        await service.AppendAsync(MakeEntry("https://example.com/x.zip"));
        await service.ClearAsync();

        var loaded = await service.LoadAsync();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadAsync_Returns_Empty_When_No_File()
    {
        var service = new DownloadHistoryService(_tempDir);
        var loaded = await service.LoadAsync();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadAsync_Returns_Empty_For_Corrupt_File()
    {
        // Write garbage JSON
        var historyPath = Path.Combine(_tempDir, "downloader-history.json");
        await File.WriteAllTextAsync(historyPath, "{ NOT VALID JSON {{{{");

        var service = new DownloadHistoryService(_tempDir);
        var loaded = await service.LoadAsync(); // must not throw

        Assert.Empty(loaded);
    }

    private static DownloadHistoryEntry MakeEntry(string url) => new()
    {
        SourceUrl = url,
        Title = url,
        FinalStatus = DownloadJobStatus.Completed,
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
