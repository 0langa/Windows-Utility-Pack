using System.Diagnostics;

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
    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);
        return (process.ExitCode, stdOut, stdErr);
    }
}