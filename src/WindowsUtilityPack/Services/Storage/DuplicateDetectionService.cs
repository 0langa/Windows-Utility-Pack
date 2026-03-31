using System.IO;
using System.Security.Cryptography;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Staged duplicate detection implementation.
/// See <see cref="IDuplicateDetectionService"/> for the algorithm description.
/// </summary>
public class DuplicateDetectionService : IDuplicateDetectionService
{
    private const long MinFileSizeForDuplicates = 1024; // Skip files smaller than 1 KB
    private const int  QuickHashBytes = 8192;            // First 8 KB for quick-hash stage

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        StorageItem root,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() => FindDuplicates(root, progress, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<DuplicateGroup> FindDuplicates(
        StorageItem root,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        // ── Stage 1: collect all files and group by size ──────────────────────
        progress?.Report("Collecting files…");
        var allFiles = new List<StorageItem>(1024);
        CollectFiles(root, allFiles, ct);

        // Group by size, keeping only groups where 2+ files have the same size
        var bySize = allFiles
            .Where(f => f.SizeBytes >= MinFileSizeForDuplicates)
            .GroupBy(f => f.SizeBytes)
            .Where(g => g.Count() > 1)
            .ToList();

        ct.ThrowIfCancellationRequested();
        progress?.Report($"Stage 1 complete: {bySize.Count} size groups with potential duplicates.");

        // ── Stage 2: quick-hash the first 8 KB of each candidate ─────────────
        progress?.Report("Computing quick hashes…");
        var quickHashCandidates = new List<(string quickHash, StorageItem file)>();

        foreach (var group in bySize)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var file in group)
            {
                var qh = ComputeQuickHash(file.FullPath, QuickHashBytes);
                if (qh != null)
                    quickHashCandidates.Add((qh, file));
            }
        }

        var byQuickHash = quickHashCandidates
            .GroupBy(x => x.quickHash)
            .Where(g => g.Count() > 1)
            .ToList();

        ct.ThrowIfCancellationRequested();
        progress?.Report($"Stage 2 complete: {byQuickHash.Count} quick-hash groups.");

        // ── Stage 3: full SHA-256 hash to confirm duplicates ──────────────────
        progress?.Report("Confirming duplicates with full hash…");
        var results = new List<DuplicateGroup>();
        int processed = 0;

        foreach (var group in byQuickHash)
        {
            ct.ThrowIfCancellationRequested();

            var fullHashGroup = new List<(string hash, StorageItem file)>();
            foreach (var (_, file) in group)
            {
                var fh = ComputeFullHash(file.FullPath);
                if (fh != null)
                    fullHashGroup.Add((fh, file));
            }

            var byFullHash = fullHashGroup
                .GroupBy(x => x.hash)
                .Where(g => g.Count() > 1);

            foreach (var dupGroup in byFullHash)
            {
                results.Add(new DuplicateGroup
                {
                    GroupKey   = dupGroup.Key,
                    Files      = dupGroup.Select(x => x.file).ToList(),
                    Confidence = DuplicateConfidence.FullHash,
                });
            }

            processed++;
            if (processed % 50 == 0)
                progress?.Report($"Processed {processed}/{byQuickHash.Count} hash groups…");
        }

        // Sort by wasted bytes descending (most impactful duplicates first)
        results.Sort((a, b) => b.WastedBytes.CompareTo(a.WastedBytes));

        progress?.Report($"Done — {results.Count} duplicate groups found.");
        return results;
    }

    private static void CollectFiles(StorageItem node, List<StorageItem> files, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
                CollectFiles(child, files, ct);
            else
                files.Add(child);
        }
    }

    private static string? ComputeQuickHash(string path, int byteCount)
    {
        try
        {
            using var fs     = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            var       buffer = new byte[byteCount];
            int       read   = fs.Read(buffer, 0, byteCount);
            using var sha    = SHA256.Create();
            var       hash   = sha.ComputeHash(buffer, 0, read);
            return Convert.ToHexString(hash);
        }
        catch { return null; }
    }

    private static string? ComputeFullHash(string path)
    {
        try
        {
            using var fs   = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            using var sha  = SHA256.Create();
            var       hash = sha.ComputeHash(fs);
            return Convert.ToHexString(hash);
        }
        catch { return null; }
    }
}
