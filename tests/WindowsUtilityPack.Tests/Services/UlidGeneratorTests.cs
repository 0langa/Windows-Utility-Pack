using WindowsUtilityPack.Services.Identifier;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class UlidGeneratorTests
{
    [Fact]
    public void Generate_Returns26CharacterUppercaseString()
    {
        var generator = new UlidGenerator();
        var value = generator.Generate();

        Assert.Equal(26, value.Length);
        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{26}$", value);
    }

    [Fact]
    public void Generate_ReturnsUniqueValues()
    {
        var generator = new UlidGenerator();
        var values = Enumerable.Range(0, 1000).Select(_ => generator.Generate()).ToList();

        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
    }
}

