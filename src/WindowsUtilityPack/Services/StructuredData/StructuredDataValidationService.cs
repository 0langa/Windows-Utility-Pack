using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace WindowsUtilityPack.Services.StructuredData;

/// <summary>
/// Default implementation of <see cref="IStructuredDataValidationService"/>.
/// </summary>
public sealed class StructuredDataValidationService : IStructuredDataValidationService
{
    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;

    public StructuredDataValidationService()
    {
        _yamlDeserializer = new DeserializerBuilder().Build();
        _yamlSerializer = new SerializerBuilder().Build();
    }

    public StructuredValidationResult Validate(string input, StructuredDocumentType documentType, Formatting formatting = Formatting.Indented)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new StructuredValidationResult
            {
                IsValid = false,
                ErrorMessage = "Input is empty.",
            };
        }

        return documentType switch
        {
            StructuredDocumentType.Json => ValidateJson(input, formatting),
            StructuredDocumentType.Yaml => ValidateYaml(input),
            _ => new StructuredValidationResult
            {
                IsValid = false,
                ErrorMessage = "Unsupported document type.",
            },
        };
    }

    private static StructuredValidationResult ValidateJson(string input, Formatting formatting)
    {
        try
        {
            var token = JToken.Parse(input);
            return new StructuredValidationResult
            {
                IsValid = true,
                NormalizedText = token.ToString(formatting),
            };
        }
        catch (JsonReaderException ex)
        {
            return new StructuredValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message,
                ErrorLine = ex.LineNumber,
                ErrorColumn = ex.LinePosition,
            };
        }
        catch (Exception ex)
        {
            return new StructuredValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    private StructuredValidationResult ValidateYaml(string input)
    {
        try
        {
            var node = _yamlDeserializer.Deserialize<object>(input);
            var normalized = _yamlSerializer.Serialize(node).TrimEnd();

            return new StructuredValidationResult
            {
                IsValid = true,
                NormalizedText = normalized,
            };
        }
        catch (YamlException ex)
        {
            return new StructuredValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message,
                ErrorLine = (int)ex.Start.Line,
                ErrorColumn = (int)ex.Start.Column,
            };
        }
        catch (Exception ex)
        {
            return new StructuredValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message,
            };
        }
    }
}
