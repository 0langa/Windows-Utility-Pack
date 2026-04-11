namespace WindowsUtilityPack.Models;

/// <summary>
/// Snapshot of process information exposed to the Process Explorer UI.
/// </summary>
public sealed class ProcessSnapshot
{
    public int ProcessId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public double WorkingSetMb { get; init; }
    public double CpuTimeSeconds { get; init; }
    public bool IsResponding { get; init; }
    public DateTime? StartTimeLocal { get; init; }

    // New: Real-time CPU% (set by ViewModel)
    public double CpuPercent { get; set; }
}