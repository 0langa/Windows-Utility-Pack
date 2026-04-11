using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class SystemInfoReportServiceTests
{
    private readonly SystemInfoReportService _service = new();

    [Fact]
    public void BuildTextReport_ContainsExpectedSections()
    {
        var snapshot = CreateSnapshot();

        var report = _service.BuildTextReport(snapshot);

        Assert.Contains("[OS]", report);
        Assert.Contains("[Memory]", report);
        Assert.Contains("[Other]", report);
        Assert.Contains("Managed Heap", report);
        Assert.Contains("Snapshot UTC", report);
    }

    [Fact]
    public void BuildJsonReport_SerializesKnownProperties()
    {
        var snapshot = CreateSnapshot();

        var json = _service.BuildJsonReport(snapshot);

        Assert.Contains("\"OsName\": \"Windows\"", json);
        Assert.Contains("\"ManagedMemory\": \"0.25 GB\"", json);
        Assert.Contains("\"SnapshotUtc\"", json);
    }

    private static SystemInfoSnapshot CreateSnapshot()
    {
        return new SystemInfoSnapshot
        {
            OsName = "Windows",
            OsVersion = "10.0.26100",
            OsBuild = "26100",
            OsDescription = "Microsoft Windows 11",
            Architecture = "X64",
            ProcessArchitecture = "X64",
            ComputerName = "TEST-PC",
            UserName = "tester",
            CpuName = "Test CPU",
            CpuCores = "8",
            CpuLogicalProcessors = "16",
            RamTotal = "16.00 GB",
            RamAvailable = "8.00 GB",
            GpuName = "Test GPU",
            SystemDrive = "C:",
            DriveSummary = "C: NTFS 120.0/256.0 GB free",
            DotNetVersion = ".NET 10.0.5",
            Uptime = "2d 3h 15m",
            ManagedMemory = "0.25 GB",
            SnapshotUtc = new DateTime(2026, 4, 11, 8, 30, 0, DateTimeKind.Utc),
        };
    }
}