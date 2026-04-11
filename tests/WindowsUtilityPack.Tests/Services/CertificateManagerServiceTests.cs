using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class CertificateManagerServiceTests
{
    [Fact]
    public void FormatPem_WrapsBytesWithHeaders()
    {
        var pem = CertificateManagerService.FormatPem([1, 2, 3, 4], "CERTIFICATE");

        Assert.Contains("BEGIN CERTIFICATE", pem);
        Assert.Contains("END CERTIFICATE", pem);
    }
}