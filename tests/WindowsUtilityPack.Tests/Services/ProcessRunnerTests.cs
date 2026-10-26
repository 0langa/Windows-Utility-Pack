using System.Diagnostics;
using System.IO;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_InvalidOutputEncodingIsNotReportedAsTimeout()
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { StandardOutputEncoding = new System.Text.UTF8Encoding(false, true) };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("[Console]::OpenStandardOutput().WriteByte(255)");
        await Assert.ThrowsAsync<System.Text.DecoderFallbackException>(() => new ProcessRunner().RunAsync(start));
    }

    [Fact]
    public async Task RunAsync_CancellationTerminatesItsOwnedChild()
    {
        var marker = Path.Combine(Path.GetTempPath(), "wup-child-" + Guid.NewGuid().ToString("N") + ".txt");
        using var cancellation = new CancellationTokenSource();
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add($"[IO.File]::WriteAllText('{marker.Replace("'", "''")}', $PID.ToString()); Start-Sleep -Seconds 60");
        Process? owned = null;
        var task = new ProcessRunner().RunAsync(start, cancellation.Token);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!File.Exists(marker) || new FileInfo(marker).Length == 0) await Task.Delay(20, deadline.Token);
            owned = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(marker)));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.True(owned.HasExited, "Canceling the runner must not leave its child alive.");
        }
        finally
        {
            cancellation.Cancel();
            // This PID was written by the child created exclusively for this test.
            if (owned is not null && !owned.HasExited) { owned.Kill(true); await owned.WaitForExitAsync(); }
            owned?.Dispose();
            File.Delete(marker);
        }
    }
}
