using WindowsUtilityPack.Services.QrCode;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Tests for <see cref="QrCodeService"/> URL normalization and safety heuristics.
/// </summary>
public sealed class QrCodeServiceTests
{
    private readonly QrCodeService _service = new();

    [Theory]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("http://example.com/path", "http://example.com/path")]
    [InlineData("www.example.com", "https://www.example.com/")]
    public void TryNormalizeUrl_AcceptsCommonInputs(string input, string expected)
    {
        var ok = _service.TryNormalizeUrl(input, out var normalized, out var error);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    [InlineData("http://localhost")]
    public void TryNormalizeUrl_RejectsUnsupportedOrInvalid(string input)
    {
        var ok = _service.TryNormalizeUrl(input, out var normalized, out var error);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void BuildSuggestedFileName_UsesDomainAndTimestampOption()
    {
        var withTimestamp = _service.BuildSuggestedFileName("https://contoso.com/docs", includeTimestamp: true);
        var withoutTimestamp = _service.BuildSuggestedFileName("https://contoso.com/docs", includeTimestamp: false);

        Assert.StartsWith("qr-contoso-com-", withTimestamp);
        Assert.EndsWith(".png", withTimestamp);
        Assert.Equal("qr-contoso-com.png", withoutTimestamp);
    }

    [Fact]
    public void AnalyzeScannability_FlagsRiskyCombinations()
    {
        var report = _service.AnalyzeScannability(new QrCodeStyleOptions
        {
            QuietZoneModules = 1,
            ForegroundColor = System.Windows.Media.Colors.Gray,
            BackgroundColor = System.Windows.Media.Colors.LightGray,
            ErrorCorrectionLevel = QrCodeErrorCorrectionLevel.Low,
            LogoScalePercent = 28,
            LogoImage = new System.Windows.Media.Imaging.WriteableBitmap(8, 8, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null),
        });

        Assert.False(report.IsLikelyScannable);
        Assert.NotEmpty(report.Warnings);
    }
}
