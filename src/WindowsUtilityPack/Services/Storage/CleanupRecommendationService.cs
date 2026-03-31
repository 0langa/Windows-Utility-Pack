using System.IO;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Implements cleanup recommendation analysis.
///
/// Identified categories (all transparent, risk-annotated):
///   1. Temporary files (*.tmp, %TEMP% folder items, Windows temp folder)
///   2. Large stale files (>100 MB and not modified in over 1 year)
///   3. Duplicate files (from a prior duplicate scan if provided)
///   4. Empty directories
///   5. Common cache-like file patterns
///
/// This service is designed to be conservative: it avoids guessing about
/// file importance in ambiguous situations and prefers marking items as
/// Medium or High risk when uncertain.
/// </summary>
public class CleanupRecommendationService : ICleanupRecommendationService
{
    private static readonly HashSet<string> TempExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".bak", ".log", ".dmp", ".old"
    };

    private static readonly HashSet<string> CacheExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cache"
    };

    private static readonly long LargeFileSizeThreshold = 100 * 1024 * 1024; // 100 MB

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CleanupRecommendation>> AnalyseAsync(
        StorageItem root,
        IReadOnlyList<DuplicateGroup>? duplicates,
        CancellationToken cancellationToken)
    {
        return await Task.Run(
            () => Analyse(root, duplicates, cancellationToken),
            cancellationToken);
    }

    private static IReadOnlyList<CleanupRecommendation> Analyse(
        StorageItem root,
        IReadOnlyList<DuplicateGroup>? duplicates,
        CancellationToken ct)
    {
        var recommendations = new List<CleanupRecommendation>();
        var tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        // Collect all files into a flat list for analysis
        var allFiles = new List<StorageItem>(512);
        CollectAll(root, allFiles, ct, includeDirectories: true);

        ct.ThrowIfCancellationRequested();

        // ── Category 1: temporary files ───────────────────────────────────────
        foreach (var item in allFiles.Where(i => !i.IsDirectory))
        {
            ct.ThrowIfCancellationRequested();

            bool inTempFolder = item.FullPath.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase);
            bool isTempExt    = TempExtensions.Contains(item.Extension);
            bool isCacheExt   = CacheExtensions.Contains(item.Extension);

            if (inTempFolder || isTempExt)
            {
                recommendations.Add(new CleanupRecommendation
                {
                    Category = CleanupCategory.TemporaryFiles,
                    Item     = item,
                    Rationale = inTempFolder
                        ? "Located in the system temporary folder."
                        : $"File has a temporary file extension ({item.Extension}).",
                    Risk     = CleanupRisk.Low,
                });
            }
            else if (isCacheExt)
            {
                recommendations.Add(new CleanupRecommendation
                {
                    Category = CleanupCategory.CacheLikeFiles,
                    Item     = item,
                    Rationale = $"File appears to be a cache file ({item.Extension}).",
                    Risk     = CleanupRisk.Medium,
                });
            }
        }

        // ── Category 2: large stale files ─────────────────────────────────────
        foreach (var item in allFiles.Where(i => !i.IsDirectory
            && i.SizeBytes >= LargeFileSizeThreshold
            && i.IsStale))
        {
            ct.ThrowIfCancellationRequested();

            // Don't double-recommend items already in temp category
            if (recommendations.Any(r => r.Item.FullPath == item.FullPath))
                continue;

            recommendations.Add(new CleanupRecommendation
            {
                Category  = CleanupCategory.LargeStaleFiles,
                Item      = item,
                Rationale = $"Large file ({item.DisplaySize}) not modified in over {item.AgeDays} days.",
                Risk      = CleanupRisk.Medium,
            });
        }

        // ── Category 3: duplicate files ───────────────────────────────────────
        if (duplicates != null)
        {
            foreach (var group in duplicates)
            {
                ct.ThrowIfCancellationRequested();

                // Recommend all but the "original" (earliest file)
                var original = group.Original;
                foreach (var file in group.Files.Where(f => f != original))
                {
                    recommendations.Add(new CleanupRecommendation
                    {
                        Category  = CleanupCategory.DuplicateFiles,
                        Item      = file,
                        Rationale = $"Duplicate of {original?.Name} — identical content confirmed by SHA-256.",
                        Risk      = CleanupRisk.Low,
                        IsSelected = false, // duplicates require explicit opt-in
                    });
                }
            }
        }

        // ── Category 4: empty directories ─────────────────────────────────────
        foreach (var item in allFiles.Where(i => i.IsDirectory
            && i.FileCount == 0
            && i.DirectoryCount == 0))
        {
            ct.ThrowIfCancellationRequested();

            recommendations.Add(new CleanupRecommendation
            {
                Category  = CleanupCategory.EmptyFolders,
                Item      = item,
                Rationale = "Empty directory with no files or subdirectories.",
                Risk      = CleanupRisk.Low,
            });
        }

        // Sort by potential savings descending
        recommendations.Sort((a, b) => b.PotentialSavingsBytes.CompareTo(a.PotentialSavingsBytes));
        return recommendations;
    }

    private static void CollectAll(
        StorageItem node,
        List<StorageItem> results,
        CancellationToken ct,
        bool includeDirectories)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
            {
                if (includeDirectories) results.Add(child);
                CollectAll(child, results, ct, includeDirectories);
            }
            else
            {
                results.Add(child);
            }
        }
    }
}
