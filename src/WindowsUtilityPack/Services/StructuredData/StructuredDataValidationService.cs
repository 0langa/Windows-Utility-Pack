using System.IO;
using YamlDotNet.Core.Events;
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
        _yamlDeserializer = new DeserializerBuilder().WithDuplicateKeyChecking().Build();
        _yamlSerializer = new SerializerBuilder().WithMaximumRecursion(64).Build();
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

        if (input.Length > 1_000_000)
            return new StructuredValidationResult { IsValid = false, ErrorMessage = "Input exceeds the 1 million character limit." };

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
            using var reader = new JsonTextReader(new StringReader(input)) { MaxDepth = 64, DateParseHandling = DateParseHandling.None };
            var token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            while (reader.Read())
                if (reader.TokenType != JsonToken.Comment) throw new JsonReaderException("Additional content after the JSON document.");
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

    private static void ValidateYamlBounds(string input)
    {
        var parser = new Parser(new StringReader(input));
        var containers = new Stack<AnchorName>();
        var events = 0;
        var aliases = 0;
        while (parser.MoveNext())
        {
            if (++events > 100_000) throw new YamlException("YAML exceeds the 100,000-node limit.");
            if (parser.Current is MappingStart or SequenceStart)
            {
                containers.Push(((NodeEvent)parser.Current).Anchor);
                if (containers.Count > 64) throw new YamlException("YAML nesting exceeds 64 levels.");
            }
            else if (parser.Current is MappingEnd or SequenceEnd) containers.Pop();
            else if (parser.Current is AnchorAlias alias)
            {
                if (++aliases > 256 || containers.Contains(alias.Value))
                    throw new YamlException("Recursive aliases and more than 256 aliases are not supported.");
            }
        }
    }

    private StructuredValidationResult ValidateYaml(string input)
    {
        try
        {
            ValidateYamlBounds(input);
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
