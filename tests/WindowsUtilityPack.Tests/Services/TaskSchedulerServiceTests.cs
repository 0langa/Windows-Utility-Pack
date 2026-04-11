using System.Diagnostics;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class TaskSchedulerServiceTests
{
    [Fact]
    public void ParseTasksFromCsv_ParsesRows()
    {
        var csv = "\"TaskName\",\"Next Run Time\",\"Status\",\"Last Run Time\",\"Last Result\",\"Author\"\n" +
                  "\"\\Test\\TaskA\",\"N/A\",\"Ready\",\"Never\",\"0\",\"User\"\n";

        var rows = TaskSchedulerService.ParseTasksFromCsv(csv);

        Assert.Single(rows);
        Assert.Equal("\\Test\\TaskA", rows[0].TaskName);
        Assert.Equal("Ready", rows[0].Status);
    }

    [Fact]
    public async Task RunTaskAsync_ReturnsTrueOnZeroExitCode()
    {
        var runner = new StubRunner((0, "ok", ""));
        var service = new TaskSchedulerService(runner);

        var ok = await service.RunTaskAsync("\\Test\\TaskA");

        Assert.True(ok);
    }

    private sealed class StubRunner : IProcessRunner
    {
        private readonly (int ExitCode, string StdOut, string StdErr) _result;

        public StubRunner((int ExitCode, string StdOut, string StdErr) result)
        {
            _result = result;
        }

        public Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }
}