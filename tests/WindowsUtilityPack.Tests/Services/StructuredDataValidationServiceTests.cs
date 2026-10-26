using WindowsUtilityPack.Services.StructuredData;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class StructuredDataValidationServiceTests
{
    [Theory]
    [InlineData("{\"key\":1,\"key\":2}", StructuredDocumentType.Json)]
    [InlineData("key: 1\nkey: 2", StructuredDocumentType.Yaml)]
    public void Validate_RejectsDuplicateKeysInsteadOfDroppingValues(string text, StructuredDocumentType type)
        => Assert.False(new StructuredDataValidationService().Validate(text, type).IsValid);

    [Theory]
    [InlineData(StructuredDocumentType.Json)]
    [InlineData(StructuredDocumentType.Yaml)]
    public void Validate_RejectsOversizedInput(StructuredDocumentType type)
        => Assert.False(new StructuredDataValidationService().Validate("\"" + new string('a', 1_000_001) + "\"", type).IsValid);

    [Theory]
    [InlineData("item: &a [*a]")]
    [InlineData("item: &a {nested: *a}")]
    public void Validate_RecursiveYamlAliasesAreRejected(string input)
        => Assert.False(new StructuredDataValidationService().Validate(input, StructuredDocumentType.Yaml).IsValid);

    [Fact]
    public void Validate_NormalAliasIsRetained()
        => Assert.True(new StructuredDataValidationService().Validate("one: &a [hello, world]\ntwo: *a", StructuredDocumentType.Yaml).IsValid);

    [Fact]
    public void Validate_JsonValid_ReturnsFormattedOutput()
    {
        var service = new StructuredDataValidationService();
        var result = service.Validate("{\"name\":\"alex\",\"count\":2}", StructuredDocumentType.Json);

        Assert.True(result.IsValid);
        Assert.Contains("\"name\": \"alex\"", result.NormalizedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_JsonInvalid_ReturnsLineAndColumn()
    {
        var service = new StructuredDataValidationService();
        var result = service.Validate("{\"name\":", StructuredDocumentType.Json);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorLine);
        Assert.NotNull(result.ErrorColumn);
    }

    [Fact]
    public void Validate_YamlValid_ReturnsNormalizedYaml()
    {
        var service = new StructuredDataValidationService();
        var result = service.Validate("name: alex\ncount: 2", StructuredDocumentType.Yaml);

        Assert.True(result.IsValid);
        Assert.Contains("name: alex", result.NormalizedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_YamlInvalid_ReturnsError()
    {
        var service = new StructuredDataValidationService();
        var result = service.Validate("name: [unclosed", StructuredDocumentType.Yaml);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }
}

