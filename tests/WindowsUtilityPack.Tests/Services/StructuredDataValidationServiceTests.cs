using WindowsUtilityPack.Services.StructuredData;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class StructuredDataValidationServiceTests
{
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

