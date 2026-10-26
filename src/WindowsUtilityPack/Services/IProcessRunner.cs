using System.Diagnostics;
using System.IO;
using System.Text;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Executes external commands and captures output.
/// </summary>
public interface IProcessRunner
{
    Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation for launching process commands.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private readonly TimeSpan _timeout;
    public ProcessRunner(TimeSpan? timeout = null) => _timeout = timeout ?? TimeSpan.FromMinutes(2);
    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(_timeout);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdOutTask = ReadBoundedAsync(process.StandardOutput, lifetime);
        var stdErrTask = ReadBoundedAsync(process.StandardError, lifetime);
        try
        {
            await process.WaitForExitAsync(lifetime.Token).ConfigureAwait(false);
            await Task.WhenAll(stdOutTask, stdErrTask).ConfigureAwait(false);
            return (process.ExitCode, await stdOutTask.ConfigureAwait(false), await stdErrTask.ConfigureAwait(false));
        }
        catch (Exception failure)
        {
            var timedOut = failure is OperationCanceledException && lifetime.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            lifetime.Cancel();
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            try { await Task.WhenAll(stdOutTask, stdErrTask).ConfigureAwait(false); } catch { /* Observe the canceled readers. */ }
            var outputFailure = new[] { stdOutTask.Exception, stdErrTask.Exception }.Where(e => e is not null)
                .SelectMany(e => e!.Flatten().InnerExceptions).FirstOrDefault(e => e is not OperationCanceledException);
            if (outputFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outputFailure).Throw();
            if (timedOut)
                throw new TimeoutException($"The command did not finish within {_timeout}.");
            throw;
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationTokenSource lifetime)
    {
        var output = new StringBuilder();
        var buffer = new char[8192];
        try
        {
            while (true)
            {
                var count = await reader.ReadAsync(buffer, lifetime.Token).ConfigureAwait(false);
                if (count == 0) return output.ToString();
                if (output.Length + count > 8 * 1024 * 1024) throw new InvalidDataException("Command output exceeds the 8 million character limit.");
                output.Append(buffer, 0, count);
            }
        }
        catch { lifetime.Cancel(); throw; }
    }
}
