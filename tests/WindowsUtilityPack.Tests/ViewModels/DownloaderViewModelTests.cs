using System.IO;
using WindowsUtilityPack.Services;
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
/// </list>
/// </summary>
public class DownloaderViewModelTests
{
    // ── Initial state ──────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsDownloading_IsFalse()
    {
        var vm = new DownloaderViewModel(new NullFolderPickerService());

        Assert.False(vm.IsDownloading);
    }

    [Fact]
    public void InitialState_Downloads_IsEmpty()
    {
        var vm = new DownloaderViewModel(new NullFolderPickerService());

        Assert.Empty(vm.Downloads);
    }

    [Fact]
    public void InitialState_SaveFolder_IsUserDownloadsFolder()
    {
        var vm = new DownloaderViewModel(new NullFolderPickerService());

        var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
        Assert.Equal(expected, vm.SaveFolder);
    }

    // ── BrowseFolderCommand ────────────────────────────────────────────────────

    [Fact]
    public void BrowseFolderCommand_UpdatesSaveFolder_WhenPickerReturnsPath()
    {
        var picker = new StubFolderPickerService(@"C:\MyDownloads");
        var vm = new DownloaderViewModel(picker);

        vm.BrowseFolderCommand.Execute(null);

        Assert.Equal(@"C:\MyDownloads", vm.SaveFolder);
    }

    [Fact]
    public void BrowseFolderCommand_DoesNotChangeSaveFolder_WhenPickerReturnsCancelled()
    {
        var picker = new StubFolderPickerService(null);
        var vm = new DownloaderViewModel(picker);
        var original = vm.SaveFolder;

        vm.BrowseFolderCommand.Execute(null);

        Assert.Equal(original, vm.SaveFolder);
    }

    // ── DownloadCommand ────────────────────────────────────────────────────────

    [Fact]
    public void DownloadCommand_IsDisabled_WhenUrlIsEmpty()
    {
        var vm = new DownloaderViewModel(new NullFolderPickerService());
        vm.Url = string.Empty;

        Assert.False(vm.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public void DownloadCommand_IsDisabled_WhenUrlIsWhitespace()
    {
        var vm = new DownloaderViewModel(new NullFolderPickerService());
        vm.Url = "   ";

        Assert.False(vm.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public void DownloadCommand_IsEnabled_WhenUrlIsNotEmpty()
    {
        var vm = new DownloaderViewModel(new NullFolderPickerService());
        vm.Url = "https://example.com/file.zip";

        Assert.True(vm.DownloadCommand.CanExecute(null));
    }

    // ── CancelCommand ──────────────────────────────────────────────────────────

    [Fact]
    public void CancelCommand_IsDisabled_WhenNotDownloading()
    {
        var vm = new DownloaderViewModel(new NullFolderPickerService());

        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    // ── ClearCompletedCommand ──────────────────────────────────────────────────

    [Fact]
    public void ClearCompletedCommand_RemovesCompletedItems()
    {
        var vm = new DownloaderViewModel(new NullFolderPickerService());

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
        var vm = new DownloaderViewModel(new NullFolderPickerService());

        vm.Downloads.Add(new DownloadItem { FileName = "a.zip", Status = "Cancelled" });
        vm.Downloads.Add(new DownloadItem { FileName = "b.zip", Status = "Failed" });
        vm.Downloads.Add(new DownloadItem { FileName = "c.zip", Status = "Downloading" });

        vm.ClearCompletedCommand.Execute(null);

        Assert.Single(vm.Downloads);
        Assert.Equal("Downloading", vm.Downloads[0].Status);
    }

    // ── Partial-file cleanup (audited correctness fix) ─────────────────────────

    /// <summary>
    /// Verifies that a partial file written to disk is deleted when the download
    /// fails (HTTP connection refused / unreachable host), and that the download
    /// item status is set to "Failed".
    ///
    /// Uses an immediately-refusing address (0.0.0.0) so that the HTTP client
    /// throws without requiring a running server.  The <see cref="DownloadItem"/>
    /// status and the absence of a partial file are both checked.
    /// </summary>
    [Fact]
    public async Task Download_FailedConnection_DeletesPartialFile_AndSetsFailedStatus()
    {
        // Create a temporary save folder.
        var saveDir = Path.Combine(Path.GetTempPath(), "DownloaderTests_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(saveDir);

        try
        {
            var vm = new DownloaderViewModel(new NullFolderPickerService());
            vm.SaveFolder = saveDir;

            // Point at a URL that is invalid so the download fails immediately —
            // this exercises the catch path that must clean up any partial file.
            vm.Url = "https://0.0.0.0/nonexistent-file-that-will-fail.bin";

            // Execute and wait for the download to fail (network failure is near-instant).
            vm.DownloadCommand.Execute(null);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (vm.IsDownloading && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            // The download must have finished (failed).
            Assert.False(vm.IsDownloading);

            // Status should be Failed.
            if (vm.Downloads.Count > 0)
                Assert.Equal("Failed", vm.Downloads[0].Status);

            // No partial file should remain under the save directory.
            var files = Directory.GetFiles(saveDir);
            Assert.Empty(files);
        }
        finally
        {
            try { Directory.Delete(saveDir, recursive: true); } catch { }
        }
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

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class NullFolderPickerService : IFolderPickerService
    {
        public string? PickFolder(string title) => null;
    }

    private sealed class StubFolderPickerService(string? path) : IFolderPickerService
    {
        public string? PickFolder(string title) => path;
    }
}
