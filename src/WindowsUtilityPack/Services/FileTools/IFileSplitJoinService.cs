namespace WindowsUtilityPack.Services.FileTools;

/// <summary>
/// Progress details for split/join operations.
/// </summary>
public sealed class FileSplitJoinProgress
{
    public long ProcessedBytes { get; init; }
    public long TotalBytes { get; init; }
    public int CurrentPart { get; init; }
    public int TotalParts { get; init; }
}

/// <summary>
/// Manifest data persisted next to generated split parts.
/// </summary>
public sealed class SplitManifest
{
    public string SourceFileName { get; init; } = string.Empty;
    public long SourceFileSizeBytes { get; init; }
    public long ChunkSizeBytes { get; init; }
    public int PartCount { get; init; }
    public string Sha256 { get; init; } = string.Empty;
}

/// <summary>
/// Split operation output details.
/// </summary>
public sealed class SplitOperationResult
{
    public required IReadOnlyList<string> PartFiles { get; init; }
    public required string ManifestPath { get; init; }
    public required string Sha256 { get; init; }
}

/// <summary>
/// Join operation output details.
/// </summary>
public sealed class JoinOperationResult
{
    public required string OutputPath { get; init; }
    public required string Sha256 { get; init; }
    public bool ChecksumVerified { get; init; }
}

/// <summary>
/// Splits large files into parts and joins part sets back to a complete file.
/// </summary>
public interface IFileSplitJoinService
{
    /// <summary>
    /// Splits <paramref name="sourceFilePath"/> into parts under <paramref name="outputDirectory"/>.
    /// </summary>
    Task<SplitOperationResult> SplitAsync(
        string sourceFilePath,
        string outputDirectory,
        long chunkSizeBytes,
        CancellationToken cancellationToken,
        IProgress<FileSplitJoinProgress>? progress = null);

    /// <summary>
    /// Joins a part set discovered from <paramref name="partFilePath"/> into <paramref name="outputFilePath"/>.
    /// </summary>
    Task<JoinOperationResult> JoinAsync(
        string partFilePath,
        string outputFilePath,
        bool verifyChecksum,
        CancellationToken cancellationToken,
        IProgress<FileSplitJoinProgress>? progress = null);
}

