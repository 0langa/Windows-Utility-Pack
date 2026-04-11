using WindowsUtilityPack.Tools.SystemUtilities.StorageMaster;
using Xunit;
using System.Globalization;

namespace WindowsUtilityPack.Tests.ViewModels;

public class StorageMasterViewModelTests
{
    [Fact]
    public void BuildFilteredResultsSummary_ReportsTruncation()
    {
        var summary = StorageMasterViewModel.BuildFilteredResultsSummary(2000, 12500);
        var expectedDisplayed = 2000.ToString("N0", CultureInfo.CurrentCulture);
        var expectedTotal = 12500.ToString("N0", CultureInfo.CurrentCulture);
        Assert.Contains($"Showing {expectedDisplayed} of {expectedTotal} files", summary);
    }

    [Fact]
    public void BuildFilteredResultsSummary_ReportsExactCountWhenNotTruncated()
    {
        var summary = StorageMasterViewModel.BuildFilteredResultsSummary(128, 128);

        Assert.Equal("128 files", summary);
    }
}
