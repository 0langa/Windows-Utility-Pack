namespace WindowsUtilityPack.Models;

/// <summary>
/// Immutable snapshot of system information used by the dashboard.
/// </summary>
public sealed class SystemInfoSnapshot
{
    public string OsName { get; init; } = string.Empty;

    public string OsVersion { get; init; } = string.Empty;

    public string OsBuild { get; init; } = string.Empty;

    public string OsDescription { get; init; } = string.Empty;

    public string Architecture { get; init; } = string.Empty;

    public string ProcessArchitecture { get; init; } = string.Empty;

    public string ComputerName { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public string CpuName { get; init; } = string.Empty;

    public string CpuCores { get; init; } = string.Empty;

    public string CpuLogicalProcessors { get; init; } = string.Empty;

    public string RamTotal { get; init; } = string.Empty;

    public string RamAvailable { get; init; } = string.Empty;

    public string GpuName { get; init; } = string.Empty;

    public string SystemDrive { get; init; } = string.Empty;

    public string DriveSummary { get; init; } = string.Empty;

    public string DotNetVersion { get; init; } = string.Empty;

    public string Uptime { get; init; } = string.Empty;

    public string ManagedMemory { get; init; } = string.Empty;

    public DateTime SnapshotUtc { get; init; }
}