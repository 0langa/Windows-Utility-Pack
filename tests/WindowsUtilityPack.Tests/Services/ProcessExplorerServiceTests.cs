using System.Diagnostics;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class ProcessExplorerServiceTests
{
    [Fact]
    public async Task GetProcessesAsync_FindsCurrentProcessWhenFilteredByName()
    {
        var service = new ProcessExplorerService();
        using var current = Process.GetCurrentProcess();

        var rows = await service.GetProcessesAsync(current.ProcessName);

        Assert.Contains(rows, p => p.ProcessId == current.Id);
    }

    [Fact]
    public async Task BuildDetailsAsync_IncludesPid()
    {
        var service = new ProcessExplorerService();
        using var current = Process.GetCurrentProcess();

        var details = await service.BuildDetailsAsync(current.Id);

        Assert.Contains($"PID: {current.Id}", details);
    }
}