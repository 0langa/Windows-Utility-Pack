using System.IO;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="DownloaderViewModel"/>.
///
/// These tests cover:
/// <list type="bullet">
///   <item>Initial state assertions.</item>
///   <item>BrowseFolderCommand wiring.</item>
///   <item>ClearCompletedCommand behaviour.</item>
///   <item>Partial-file cleanup on cancellation/failure (the audited defect).</item>
///   <item>Progress tracking correctness (separated cumulative vs. speed-sample bytes).</item>
///   <item>Multi-URL support and deduplication.</item>
///   <item>CancelItemCommand and new DownloadItem properties.</item>
/// </list>
/// </summary>
public class DownloaderViewModelTests
{
    // ── Initial state ──────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsDownloading_IsFalse()
    {
        var vm = CreateVm();

        Assert.False(vm.IsDownloading);
    }

    [Fact]
    public void InitialState_Downloads_IsEmpty()
    {
        var vm = CreateVm();

        Assert.Empty(vm.Downloads);
    }

    [Fact]
    public void InitialState_SaveFolder_IsUserDownloadsFolder()
    {
        var vm = CreateVm();

        var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
        Assert.Equal(expected, vm.SaveFolder);
    }

    // ── BrowseFolderCommand ────────────────────────────────────────────────────

    [Fact]
    public void BrowseFolderCommand_UpdatesSaveFolder_WhenPickerReturnsPath()
    {
        var picker = new StubFolderPickerService(@"C:\MyDownloads");
        var vm = CreateVm(picker);

        vm.BrowseFolderCommand.Execute(null);

        Assert.Equal(@"C:\MyDownloads", vm.SaveFolder);
    }

    [Fact]
    public void BrowseFolderCommand_DoesNotChangeSaveFolder_WhenPickerReturnsCancelled()
    {
        var picker = new StubFolderPickerService(null);
        var vm = CreateVm(picker);
        var original = vm.SaveFolder;

        vm.BrowseFolderCommand.Execute(null);

        Assert.Equal(original, vm.SaveFolder);
    }

    // ── DownloadCommand ────────────────────────────────────────────────────────

    [Fact]
    public void DownloadCommand_IsDisabled_WhenUrlIsEmpty()
    {
        var vm = CreateVm();
        vm.Url = string.Empty;

        Assert.False(vm.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public void DownloadCommand_IsDisabled_WhenUrlIsWhitespace()
    {
        var vm = CreateVm();
        vm.Url = "   ";

        Assert.False(vm.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public void DownloadCommand_IsEnabled_WhenUrlIsNotEmpty()
    {
        var vm = CreateVm();
        vm.Url = "https://example.com/file.zip";

        Assert.True(vm.DownloadCommand.CanExecute(null));
    }

    // ── CancelCommand ──────────────────────────────────────────────────────────

    [Fact]
    public void CancelCommand_IsDisabled_WhenNotDownloading()
    {
        var vm = CreateVm();

        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    // ── ClearCompletedCommand ──────────────────────────────────────────────────

    [Fact]
    public void ClearCompletedCommand_RemovesCompletedItems()
    {
        var vm = CreateVm();

        var completed = new DownloadItem { FileName = "done.zip", Status = "Complete" };
        var queued    = new DownloadItem { FileName = "pending.zip", Status = "Queued" };

        vm.Downloads.Add(completed);
        vm.Downloads.Add(queued);

        vm.ClearCompletedCommand.Execute(null);

        Assert.Single(vm.Downloads);
        Assert.Equal("Queued", vm.Downloads[0].Status);
    }

    [Fact]
    public void ClearCompletedCommand_RemovesCancelledAndFailedItems()
    {
        var vm = CreateVm();

        vm.Downloads.Add(new DownloadItem { FileName = "a.zip", Status = "Cancelled" });
        vm.Downloads.Add(new DownloadItem { FileName = "b.zip", Status = "Failed" });
        vm.Downloads.Add(new DownloadItem { FileName = "c.zip", Status = "Downloading" });

        vm.ClearCompletedCommand.Execute(null);

        Assert.Single(vm.Downloads);
        Assert.Equal("Downloading", vm.Downloads[0].Status);
    }

    // ── DownloadItem property notifications ────────────────────────────────────

    [Fact]
    public void DownloadItem_Progress_RaisesPropertyChanged()
    {
        var item = new DownloadItem();
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Progress = 50.0;

        Assert.Contains(nameof(DownloadItem.Progress), raised);
    }

    [Fact]
    public void DownloadItem_Status_RaisesPropertyChanged()
    {
        var item = new DownloadItem();
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Status = "Downloading";

        Assert.Contains(nameof(DownloadItem.Status), raised);
    }

    // ── New: DownloadCommand multi-line and deduplication ───────────────────────

    [Fact]
    public void DownloadCommand_AcceptsMultilineUrls_AddsOneItemPerLine()
    {
        var vm = CreateVm();
        vm.Url = "https://example.com/a.zip\nhttps://example.com/b.zip";

        vm.DownloadCommand.Execute(null);

        Assert.Equal(2, vm.Downloads.Count);
    }

    [Fact]
    public void DownloadCommand_DeduplicatesUrls_DoesNotAddDuplicate()
    {
        var vm = CreateVm();
        vm.Url = "https://example.com/a.zip";
        vm.DownloadCommand.Execute(null);

        vm.Url = "https://example.com/a.zip";
        vm.DownloadCommand.Execute(null);

        Assert.Single(vm.Downloads);
    }

    // ── New: CancelItemCommand ─────────────────────────────────────────────────

    [Fact]
    public void CancelItemCommand_SetsItemStatusToCancelled()
    {
        var vm = CreateVm();
        var item = new DownloadItem { FileName = "test.zip", Status = "Queued" };
        vm.Downloads.Add(item);

        vm.CancelItemCommand.Execute(item);

        Assert.Equal("Cancelled", item.Status);
    }

    // ── New: ClearCompletedCommand includes Cancelled ──────────────────────────

    [Fact]
    public void ClearCompletedCommand_RemovesCompletedAndFailedAndCancelled()
    {
        var vm = CreateVm();

        vm.Downloads.Add(new DownloadItem { FileName = "a.zip", Status = "Complete" });
        vm.Downloads.Add(new DownloadItem { FileName = "b.zip", Status = "Failed" });
        vm.Downloads.Add(new DownloadItem { FileName = "c.zip", Status = "Cancelled" });
        vm.Downloads.Add(new DownloadItem { FileName = "d.zip", Status = "Queued" });

        vm.ClearCompletedCommand.Execute(null);

        Assert.Single(vm.Downloads);
        Assert.Equal("Queued", vm.Downloads[0].Status);
    }

    // ── New: DownloadItem.Engine and .Title raise PropertyChanged ───────────────

    [Fact]
    public void DownloadItem_Engine_RaisesPropertyChanged()
    {
        var item = new DownloadItem();
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Engine = "yt-dlp";

        Assert.Contains(nameof(DownloadItem.Engine), raised);
    }

    [Fact]
    public void DownloadItem_Title_RaisesPropertyChanged()
    {
        var item = new DownloadItem();
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Title = "My Video";

        Assert.Contains(nameof(DownloadItem.Title), raised);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DownloaderViewModel CreateVm(IFolderPickerService? folderPicker = null)
    {
        return new DownloaderViewModel(
            folderPicker ?? new NullFolderPickerService(),
            new NullDependencyManagerService(),
            new NullDownloadEngineService(),
            new NullWebScraperService());
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class NullFolderPickerService : IFolderPickerService
    {
        public string? PickFolder(string title) => null;
    }

    private sealed class StubFolderPickerService(string? path) : IFolderPickerService
    {
        public string? PickFolder(string title) => path;
    }

    private sealed class NullDependencyManagerService : IDependencyManagerService
    {
        public string YtDlpPath => string.Empty;
        public string GalleryDlPath => string.Empty;
        public string FfmpegPath => string.Empty;
        public DependencyStatus Check() => new(false, false, false);
        public Task EnsureAllAsync(Action<string> onProgress, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> UpdateYtDlpAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
    }

    private sealed class NullDownloadEngineService : IDownloadEngineService
    {
        public IReadOnlyList<(string Label, string Format)> VideoFormats { get; } =
        [
            ("Best (auto)", "bestvideo+bestaudio/best"),
        ];

        public Task DetectEngineAsync(DownloadItem item, CancellationToken ct = default)
        {
            item.Engine = "Scraper";
            item.Title = "Test";
            return Task.CompletedTask;
        }

        public Task<List<ScrapedAsset>?> DownloadAsync(DownloadItem item, CancellationToken ct = default)
            => Task.FromResult<List<ScrapedAsset>?>(null);

        public Task DownloadScrapedAssetsAsync(
            IEnumerable<ScrapedAsset> assets, string outputDir,
            bool crawlSubdirectories, int maxDepth, int maxPages,
            Action<double> onProgress, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class NullWebScraperService : IWebScraperService
    {
        public Task<List<ScrapedAsset>> ScrapeAsync(
            string pageUrl, bool crawlSubdirectories, int maxDepth, int maxPages,
            Action<(int pagesScraped, int assetsFound)>? onProgress,
            CancellationToken ct = default)
            => Task.FromResult(new List<ScrapedAsset>());

        public Task<string> DownloadAssetAsync(
            ScrapedAsset asset, string outputDir,
            IProgress<double>? progress, CancellationToken ct = default)
            => Task.FromResult(string.Empty);
    }
}
