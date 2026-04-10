using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;

namespace WindowsUtilityPack.Services.FileTools;

/// <summary>
/// File split/join implementation with manifest-based checksum verification.
/// </summary>
public sealed class FileSplitJoinService : IFileSplitJoinService
{
    private const int BufferSize = 1024 * 256;
    private static readonly Regex PartFileRegex = new(@"\.part(?<index>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<SplitOperationResult> SplitAsync(
        string sourceFilePath,
        string outputDirectory,
        long chunkSizeBytes,
        CancellationToken cancellationToken,
        IProgress<FileSplitJoinProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("Source file is required.", nameof(sourceFilePath));
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file not found.", sourceFilePath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        if (chunkSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSizeBytes), "Chunk size must be greater than zero.");

        Directory.CreateDirectory(outputDirectory);

        var sourceInfo = new FileInfo(sourceFilePath);
        var totalBytes = sourceInfo.Length;
        var totalParts = (int)Math.Ceiling(totalBytes / (double)chunkSizeBytes);
        var baseName = Path.GetFileName(sourceFilePath);

        var partFiles = new List<string>(Math.Max(totalParts, 1));
        var buffer = new byte[BufferSize];
        long processedBytes = 0;
        var partNumber = 0;

        using var source = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        while (processedBytes < totalBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            partNumber++;
            var partPath = Path.Combine(outputDirectory, $"{baseName}.part{partNumber:D4}");
            partFiles.Add(partPath);

            await using var partStream = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            var remainingForPart = chunkSizeBytes;

            while (remainingForPart > 0 && processedBytes < totalBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var toRead = (int)Math.Min(Math.Min(buffer.Length, remainingForPart), totalBytes - processedBytes);
                var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                    break;

                await partStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);

                processedBytes += read;
                remainingForPart -= read;

                progress?.Report(new FileSplitJoinProgress
                {
                    ProcessedBytes = processedBytes,
                    TotalBytes = totalBytes,
                    CurrentPart = partNumber,
                    TotalParts = totalParts,
                });
            }
        }

        var sha256 = Convert.ToHexString(hash.GetHashAndReset());
        var manifest = new SplitManifest
        {
            SourceFileName = baseName,
            SourceFileSizeBytes = totalBytes,
            ChunkSizeBytes = chunkSizeBytes,
            PartCount = partFiles.Count,
            Sha256 = sha256,
        };

        var manifestPath = Path.Combine(outputDirectory, $"{baseName}.manifest.json");
        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken).ConfigureAwait(false);

        return new SplitOperationResult
        {
            PartFiles = partFiles,
            ManifestPath = manifestPath,
            Sha256 = sha256,
        };
    }

    public async Task<JoinOperationResult> JoinAsync(
        string partFilePath,
        string outputFilePath,
        bool verifyChecksum,
        CancellationToken cancellationToken,
        IProgress<FileSplitJoinProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(partFilePath))
            throw new ArgumentException("Part file is required.", nameof(partFilePath));
        if (!File.Exists(partFilePath))
            throw new FileNotFoundException("Part file not found.", partFilePath);
        if (string.IsNullOrWhiteSpace(outputFilePath))
            throw new ArgumentException("Output file is required.", nameof(outputFilePath));

        var orderedParts = ResolvePartSet(partFilePath);
        if (orderedParts.Count == 0)
            throw new InvalidOperationException("No part files were found for the selected input.");

        var totalBytes = orderedParts.Sum(path => new FileInfo(path).Length);
        var processedBytes = 0L;
        var buffer = new byte[BufferSize];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var outputDir = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        await using (var output = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
        {
            for (var index = 0; index < orderedParts.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var partPath = orderedParts[index];
                await using var input = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read <= 0)
                        break;

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    hash.AppendData(buffer, 0, read);
                    processedBytes += read;

                    progress?.Report(new FileSplitJoinProgress
                    {
                        ProcessedBytes = processedBytes,
                        TotalBytes = totalBytes,
                        CurrentPart = index + 1,
                        TotalParts = orderedParts.Count,
                    });
                }
            }
        }

        var joinedSha256 = Convert.ToHexString(hash.GetHashAndReset());
        var checksumVerified = false;

        if (verifyChecksum)
        {
            var manifest = TryLoadManifest(partFilePath);
            if (manifest is not null && !string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                checksumVerified = string.Equals(manifest.Sha256, joinedSha256, StringComparison.OrdinalIgnoreCase);
                if (!checksumVerified)
                    throw new InvalidOperationException("Checksum verification failed. Reconstructed file does not match manifest hash.");
            }
        }

        return new JoinOperationResult
        {
            OutputPath = outputFilePath,
            Sha256 = joinedSha256,
            ChecksumVerified = checksumVerified,
        };
    }

    private static List<string> ResolvePartSet(string selectedPartPath)
    {
        var directory = Path.GetDirectoryName(selectedPartPath) ?? string.Empty;
        var fileName = Path.GetFileName(selectedPartPath);
        var match = PartFileRegex.Match(fileName);
        if (!match.Success)
            throw new InvalidOperationException("Selected file is not a part file (expected *.partNNNN).");

        var baseName = fileName[..match.Index];
        return Directory.GetFiles(directory, $"{baseName}.part*")
            .Select(path => new { Path = path, Index = ExtractPartIndex(path) })
            .Where(item => item.Index > 0)
            .OrderBy(item => item.Index)
            .Select(item => item.Path)
            .ToList();
    }

    private static int ExtractPartIndex(string path)
    {
        var fileName = Path.GetFileName(path);
        var match = PartFileRegex.Match(fileName);
        if (!match.Success)
            return -1;

        return int.TryParse(match.Groups["index"].Value, out var index) ? index : -1;
    }

    private static SplitManifest? TryLoadManifest(string partFilePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(partFilePath) ?? string.Empty;
            var fileName = Path.GetFileName(partFilePath);
            var match = PartFileRegex.Match(fileName);
            if (!match.Success)
                return null;

            var baseName = fileName[..match.Index];
            var manifestPath = Path.Combine(directory, $"{baseName}.manifest.json");
            if (!File.Exists(manifestPath))
                return null;

            var json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<SplitManifest>(json);
        }
        catch
        {
            return null;
        }
    }
}
