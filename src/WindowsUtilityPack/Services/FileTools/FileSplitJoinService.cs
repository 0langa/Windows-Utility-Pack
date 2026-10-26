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

        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(outputDirectory);

        var sourceInfo = new FileInfo(sourceFilePath);
        var totalBytes = sourceInfo.Length;
        var partCount = Math.Max(1, totalBytes / chunkSizeBytes + (totalBytes % chunkSizeBytes == 0 ? 0 : 1));
        if (partCount > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(chunkSizeBytes), "Chunk size would create too many parts.");
        var totalParts = (int)partCount;
        var baseName = Path.GetFileName(sourceFilePath);
        var manifestPath = Path.Combine(outputDirectory, $"{baseName}.manifest.json");
        // A part set is a unit: never mix a new split with an existing set.
        if (File.Exists(manifestPath) || Directory.EnumerateFiles(outputDirectory, $"{baseName}.part*").Any())
            throw new IOException("A part set already exists in this folder. Choose an empty output folder.");

        var partFiles = new List<string>(Math.Max(totalParts, 1));
        var buffer = new byte[BufferSize];
        long processedBytes = 0;
        var partNumber = 0;

        using var source = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        if (source.Length != totalBytes)
            throw new IOException("The source file changed before splitting started. Retry the operation.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var createdFiles = new List<string>();
        try
        {
            while (partNumber < totalParts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                partNumber++;
                var partPath = Path.Combine(outputDirectory, $"{baseName}.part{partNumber:D4}");
                await using var partStream = new FileStream(partPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
                createdFiles.Add(partPath);
                partFiles.Add(partPath);
                var remainingForPart = chunkSizeBytes;

                while (remainingForPart > 0 && processedBytes < totalBytes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var toRead = (int)Math.Min(Math.Min(buffer.Length, remainingForPart), totalBytes - processedBytes);
                    var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
                    if (read <= 0)
                        throw new EndOfStreamException("The source file changed while it was being split.");

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

            await using (var manifestStream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                createdFiles.Add(manifestPath);
                await JsonSerializer.SerializeAsync(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true }, cancellationToken).ConfigureAwait(false);
            }

            return new SplitOperationResult
            {
                PartFiles = partFiles,
                ManifestPath = manifestPath,
                Sha256 = sha256,
            };
        }
        catch
        {
            foreach (var path in createdFiles) TryDeleteIncompleteFile(path);
            throw;
        }
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

        cancellationToken.ThrowIfCancellationRequested();
        partFilePath = Path.GetFullPath(partFilePath);
        outputFilePath = Path.GetFullPath(outputFilePath);

        var orderedParts = ResolvePartSet(partFilePath);
        if (orderedParts.Count == 0)
            throw new InvalidOperationException("No part files were found for the selected input.");

        if (orderedParts.Any(path => string.Equals(Path.GetFullPath(path), outputFilePath, StringComparison.OrdinalIgnoreCase))
            || string.Equals(GetManifestPath(partFilePath), outputFilePath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The output must not replace an input part or its manifest.");

        for (var index = 0; index < orderedParts.Count; index++)
            if (ExtractPartIndex(orderedParts[index]) != index + 1)
                throw new InvalidDataException("The part set has missing or duplicate part numbers.");

        var totalBytes = orderedParts.Sum(path => new FileInfo(path).Length);
        var manifest = LoadManifest(partFilePath);
        if (verifyChecksum && (manifest is null || !Regex.IsMatch(manifest.Sha256, "\\A[0-9a-fA-F]{64}\\z")))
            throw new InvalidDataException("A valid SHA-256 manifest is required for checksum verification.");
        if (manifest is not null)
        {
            if (manifest.PartCount != orderedParts.Count || manifest.SourceFileSizeBytes != totalBytes || manifest.ChunkSizeBytes <= 0)
                throw new InvalidDataException("The part set does not match the manifest count or size.");
            for (var index = 0; index < orderedParts.Count; index++)
            {
                var expected = Math.Min(manifest.ChunkSizeBytes, totalBytes - index * manifest.ChunkSizeBytes);
                if (new FileInfo(orderedParts[index]).Length != expected)
                    throw new InvalidDataException("A part size does not match the manifest.");
            }
        }
        var processedBytes = 0L;
        var buffer = new byte[BufferSize];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var outputDir = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        var temporaryPath = Path.Combine(outputDir!, $".wup-join-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
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
                checksumVerified = string.Equals(manifest!.Sha256, joinedSha256, StringComparison.OrdinalIgnoreCase);
                if (!checksumVerified)
                    throw new InvalidOperationException("Checksum verification failed. Reconstructed file does not match manifest hash.");
            }

            if (processedBytes != totalBytes)
                throw new InvalidDataException("The part set changed while it was being joined.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, outputFilePath, overwrite: true);

            return new JoinOperationResult
            {
                OutputPath = outputFilePath,
                Sha256 = joinedSha256,
                ChecksumVerified = checksumVerified,
            };
        }
        finally
        {
            TryDeleteIncompleteFile(temporaryPath);
        }
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

    private static string GetManifestPath(string partFilePath)
    {
        var fileName = Path.GetFileName(partFilePath);
        var baseName = fileName[..PartFileRegex.Match(fileName).Index];
        return Path.Combine(Path.GetDirectoryName(partFilePath)!, $"{baseName}.manifest.json");
    }

    private static SplitManifest? LoadManifest(string partFilePath)
    {
        var manifestPath = GetManifestPath(partFilePath);
        if (!File.Exists(manifestPath)) return null;
        return JsonSerializer.Deserialize<SplitManifest>(File.ReadAllText(manifestPath))
            ?? throw new InvalidDataException("The manifest is empty or invalid.");
    }

    private static void TryDeleteIncompleteFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
