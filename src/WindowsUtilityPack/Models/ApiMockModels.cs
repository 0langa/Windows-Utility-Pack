namespace WindowsUtilityPack.Models;

/// <summary>
/// Configured mock endpoint definition.
/// </summary>
public sealed class ApiMockEndpoint
{
    public string Name { get; set; } = "New endpoint";

    public string Method { get; set; } = "GET";

    public string Path { get; set; } = "/";

    public int StatusCode { get; set; } = 200;

    public string ContentType { get; set; } = "application/json";

    public string ResponseBody { get; set; } = "{\"ok\":true}";

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Request log entry captured by the API mock server.
/// </summary>
public sealed class ApiMockRequestLogEntry
{
    public DateTime TimestampUtc { get; init; }

    public string Method { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public int StatusCode { get; init; }

    public string EndpointName { get; init; } = string.Empty;
}