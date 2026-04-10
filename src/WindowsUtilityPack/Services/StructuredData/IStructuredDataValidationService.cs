using Newtonsoft.Json;

namespace WindowsUtilityPack.Services.StructuredData;

/// <summary>
/// Supported structured document types for validation and formatting.
/// </summary>
public enum StructuredDocumentType
{
    Json,
    Yaml,
}

/// <summary>
/// Validation output for structured text inputs.
/// </summary>
public sealed class StructuredValidationResult
{
    public bool IsValid { get; init; }
    public string NormalizedText { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public int? ErrorLine { get; init; }
    public int? ErrorColumn { get; init; }
}

/// <summary>
/// Validates and formats JSON or YAML payloads.
/// </summary>
public interface IStructuredDataValidationService
{
    /// <summary>
    /// Validates the input for the selected document type and returns formatted output or parse diagnostics.
    /// </summary>
    StructuredValidationResult Validate(string input, StructuredDocumentType documentType, Formatting formatting = Formatting.Indented);
}

