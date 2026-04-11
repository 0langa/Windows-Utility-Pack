using System.Diagnostics;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Provides process listing and control operations for Process Explorer.
/// </summary>
public interface IProcessExplorerService
{
    Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(string? query = null, CancellationToken cancellationToken = default);

    Task<string> BuildDetailsAsync(int processId, CancellationToken cancellationToken = default);

    Task<bool> TryTerminateAsync(int processId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default process explorer implementation using System.Diagnostics.Process.
/// </summary>
public sealed class ProcessExplorerService : IProcessExplorerService
{
    public Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ProcessSnapshot>>(() =>
        {
            var filter = query?.Trim();
            var processes = Process.GetProcesses();
            var snapshots = new List<ProcessSnapshot>(processes.Length);

            foreach (var process in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var item = ToSnapshot(process);
                    if (!Matches(item, filter))
                    {
                        continue;
                    }

                    snapshots.Add(item);
                }
                catch
                {
                    // Ignore inaccessible process entries.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return snapshots
                .OrderByDescending(p => p.WorkingSetMb)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.ProcessId)
                .ToList();
        }, cancellationToken);
    }

    public Task<string> BuildDetailsAsync(int processId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var process = Process.GetProcessById(processId);
            var snapshot = ToSnapshot(process);

            return string.Join(Environment.NewLine,
            [
                $"Name: {snapshot.Name}",
                $"PID: {snapshot.ProcessId}",
                $"Path: {snapshot.ExecutablePath}",
                $"Memory (MB): {snapshot.WorkingSetMb:F1}",
                $"CPU Time (s): {snapshot.CpuTimeSeconds:F1}",
                $"Responding: {snapshot.IsResponding}",
                $"Started: {(snapshot.StartTimeLocal?.ToString("G") ?? "n/a")}",
            ]);
        }, cancellationToken);
    }

    public Task<bool> TryTerminateAsync(int processId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var process = Process.GetProcessById(processId);
                process.Kill(entireProcessTree: false);
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    private static bool Matches(ProcessSnapshot snapshot, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        if (snapshot.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (snapshot.ExecutablePath.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return snapshot.ProcessId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessSnapshot ToSnapshot(Process process)
    {
        string executablePath;
        try
        {
            executablePath = process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            executablePath = string.Empty;
        }

        DateTime? startTime = null;
        try
        {
            startTime = process.StartTime;
        }
        catch
        {
            // Ignore processes where start time is unavailable.
        }

        return new ProcessSnapshot
        {
            ProcessId = process.Id,
            Name = process.ProcessName,
            ExecutablePath = executablePath,
            WorkingSetMb = Math.Max(0, process.WorkingSet64 / 1_048_576d),
            CpuTimeSeconds = Math.Max(0, process.TotalProcessorTime.TotalSeconds),
            IsResponding = SafeResponding(process),
            StartTimeLocal = startTime,
        };
    }

    private static bool SafeResponding(Process process)
    {
        try
        {
            return process.Responding;
        }
        catch
        {
            return false;
        }
    }
}