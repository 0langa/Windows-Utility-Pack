using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage
{
    /// <summary>
    /// Detects duplicate files within a directory tree using SHA-256 content hashing.
    /// Files are first grouped by size (cheap) then hashed (expensive) to minimise I/O.
    /// </summary>
    public sealed class DuplicateDetectionService : IDuplicateDetectionService
    {
        private const int BufferSize = 81_920; // 80 KB streaming buffer

        public async Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
            string rootPath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("Root path must not be empty.", nameof(rootPath));

            if (!Directory.Exists(rootPath))
                throw new DirectoryNotFoundException($"Directory not found: {rootPath}");

            // Step 1 — enumerate all files
            var allFiles = await Task.Run(
                () => Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                               .ToList(),
                cancellationToken);

            if (allFiles.Count == 0)
                return Array.Empty<DuplicateGroup>();

            // Step 2 — group by size (instant filter; files with unique sizes cannot be duplicates)
            var sizeGroups = allFiles
                .Select(f => new FileInfo(f))
                .Where(fi => fi.Exists && fi.Length > 0)
                .GroupBy(fi => fi.Length)
                .Where(g => g.Count() > 1)
                .ToList();

            var results = new List<DuplicateGroup>();
            int processed = 0;
            int total = sizeGroups.Sum(g => g.Count());

            // Step 3 — hash each candidate group
            foreach (var sizeGroup in sizeGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hashGroups = new Dictionary<string, List<FileInfo>>();

                foreach (var fi in sizeGroup)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        string hash = await ComputeHashAsync(fi.FullName, cancellationToken)
                            .ConfigureAwait(false);

                        if (!hashGroups.TryGetValue(hash, out var list))
                            hashGroups[hash] = list = new List<FileInfo>();

                        list.Add(fi);
                    }
                    catch (IOException) { /* skip locked / inaccessible files */ }
                    catch (UnauthorizedAccessException) { /* skip files we can't read */ }
                    finally
                    {
                        progress?.Report((int)(++processed * 100.0 / total));
                    }
                }

                // Any hash bucket with 2+ files is a duplicate group
                foreach (var kvp in hashGroups.Where(kv => kv.Value.Count > 1))
                {
                    results.Add(new DuplicateGroup(
                        kvp.Key,
                       kvp.Value.Select(fi => new StorageItem
                       {
                           FullPath     = fi.FullName,
                           Name         = fi.Name,
                           SizeBytes    = fi.Length,
                           LastModified = fi.LastWriteTime,
                           CreatedAt    = fi.CreationTime,
                           Attributes   = fi.Attributes,
                       }).ToList()));
                }
            }

            return results;
        }

        // ── private helpers ──────────────────────────────────────────────────────

        private static async Task<string> ComputeHashAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                useAsync: true);

            byte[] hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken)
                .ConfigureAwait(false);

            return Convert.ToHexString(hashBytes); // .NET 5+ built-in, no allocations
        }
    }
}
