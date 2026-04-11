using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class StartupDiagnosticsServiceTests
{
    private readonly StartupDiagnosticsService _service = new();

    [Fact]
    public void Summarize_ComputesExpectedCounts()
    {
        var entries = new List<StartupEntryDiagnostic>
        {
            new()
            {
                Name = "One",
                IsEnabled = true,
                Source = "HKCU",
                TargetExists = true,
                IsPotentiallyRisky = false,
            },
            new()
            {
                Name = "Two",
                IsEnabled = false,
                Source = "HKLM",
                TargetExists = false,
                IsPotentiallyRisky = true,
            },
        };

        var summary = _service.Summarize(entries);

        Assert.Equal(2, summary.TotalEntries);
        Assert.Equal(1, summary.EnabledEntries);
        Assert.Equal(1, summary.DisabledEntries);
        Assert.Equal(1, summary.MissingTargetEntries);
        Assert.Equal(1, summary.RiskFlaggedEntries);
        Assert.Equal(1, summary.MachineScopeEntries);
    }

    [Fact]
    public void BuildCsv_EscapesQuotedAndCommaText()
    {
        var entries = new List<StartupEntryDiagnostic>
        {
            new()
            {
                Name = "Test",
                Command = "\"C:\\Program Files\\App\\app.exe\",-flag",
                IsEnabled = true,
                Source = "HKCU",
                ExecutablePath = "C:\\Program Files\\App\\app.exe",
                TargetExists = true,
                IsPotentiallyRisky = false,
            },
        };

        var csv = _service.BuildCsv(entries);

        Assert.Contains("\"\"C:\\Program Files\\App\\app.exe\"\",-flag\"", csv);
        Assert.Contains("Name,Command,Enabled,Source,ExecutablePath,TargetExists,RiskFlagged", csv);
    }

    [Fact]
    public void BuildDiagnosticsReport_IncludesSummaryAndRiskSection()
    {
        var entries = new List<StartupEntryDiagnostic>
        {
            new()
            {
                Name = "Risky",
                Command = "powershell -enc abc",
                IsEnabled = true,
                Source = "HKCU",
                TargetExists = true,
                IsPotentiallyRisky = true,
            },
        };

        var report = _service.BuildDiagnosticsReport(entries, hklmEntriesSkipped: true);

        Assert.Contains("Startup Manager Diagnostics", report);
        Assert.Contains("Risk-flagged entries", report);
        Assert.Contains("Risky", report);
        Assert.Contains("HKLM read skipped: yes", report);
    }
}