using WindowsUtilityPack.Tools.SecurityPrivacy.LocalSecretVault;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class LocalSecretVaultViewModelTests
{
    [Theory]
    [InlineData(1, 250)]
    [InlineData(2, 500)]
    [InlineData(3, 1000)]
    [InlineData(4, 2000)]
    public void UnlockBackoffDelay_GrowsExponentially(int attempts, int expectedMs)
    {
        var delay = LocalSecretVaultViewModel.GetUnlockBackoffDelay(attempts);

        Assert.Equal(expectedMs, (int)delay.TotalMilliseconds);
    }

    [Fact]
    public void UnlockBackoffDelay_IsCapped()
    {
        var delay = LocalSecretVaultViewModel.GetUnlockBackoffDelay(20);

        Assert.Equal(10_000, (int)delay.TotalMilliseconds);
    }

    [Fact]
    public void ClearSensitiveBuffer_ZeroesContents()
    {
        var bytes = new byte[] { 10, 20, 30, 40 };

        LocalSecretVaultViewModel.ClearSensitiveBuffer(bytes);

        Assert.All(bytes, value => Assert.Equal((byte)0, value));
    }
}
