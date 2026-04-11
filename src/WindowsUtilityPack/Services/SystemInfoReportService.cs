using System.Text;
using System.Text.Json;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Produces export payloads for system information snapshots.
/// </summary>
public interface ISystemInfoReportService
{
    string BuildTextReport(SystemInfoSnapshot snapshot);

    string BuildJsonReport(SystemInfoSnapshot snapshot);
}

/// <inheritdoc/>
public sealed class SystemInfoReportService : ISystemInfoReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string BuildTextReport(SystemInfoSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sb = new StringBuilder();
        sb.AppendLine("=== System Information ===");
        sb.AppendLine();
        sb.AppendLine("[OS]");
        sb.AppendLine($"  Name                 : {snapshot.OsName}");
        sb.AppendLine($"  Description          : {snapshot.OsDescription}");
        sb.AppendLine($"  Version              : {snapshot.OsVersion}");
        sb.AppendLine($"  Build                : {snapshot.OsBuild}");
        sb.AppendLine($"  Architecture         : {snapshot.Architecture}");
        sb.AppendLine($"  Process Architecture : {snapshot.ProcessArchitecture}");
        sb.AppendLine();
        sb.AppendLine("[Computer]");
        sb.AppendLine($"  Name         : {snapshot.ComputerName}");
        sb.AppendLine($"  User         : {snapshot.UserName}");
        sb.AppendLine($"  System Drive : {snapshot.SystemDrive}");
        sb.AppendLine();
        sb.AppendLine("[CPU]");
        sb.AppendLine($"  Name              : {snapshot.CpuName}");
        sb.AppendLine($"  Cores             : {snapshot.CpuCores}");
        sb.AppendLine($"  Logical Processors: {snapshot.CpuLogicalProcessors}");
        sb.AppendLine();
        sb.AppendLine("[Memory]");
        sb.AppendLine($"  Total          : {snapshot.RamTotal}");
        sb.AppendLine($"  Available      : {snapshot.RamAvailable}");
        sb.AppendLine($"  Managed Heap   : {snapshot.ManagedMemory}");
        sb.AppendLine();
        sb.AppendLine("[Other]");
        sb.AppendLine($"  .NET Version : {snapshot.DotNetVersion}");
        sb.AppendLine($"  GPU          : {snapshot.GpuName}");
        sb.AppendLine($"  Uptime       : {snapshot.Uptime}");
        sb.AppendLine($"  Drives       : {snapshot.DriveSummary}");
        sb.AppendLine();
        sb.AppendLine($"Snapshot UTC   : {snapshot.SnapshotUtc:yyyy-MM-dd HH:mm:ss}");
        return sb.ToString();
    }

    public string BuildJsonReport(SystemInfoSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }
}