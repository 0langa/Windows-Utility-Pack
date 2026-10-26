using System.IO;
using System.Security.Cryptography;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

public static class CleanupCandidateValidator
{
    public static void Validate(CleanupRecommendation recommendation, IReadOnlyCollection<CleanupRecommendation>? selected = null)
    {
        using var lease = AcquireDeletionLease(recommendation, selected);
    }

    /// <summary>Keeps a verified surviving duplicate open against writes/deletion until cleanup finishes.</summary>
    public static IDisposable AcquireDeletionLease(CleanupRecommendation recommendation, IReadOnlyCollection<CleanupRecommendation>? selected = null)
    {
        var item = recommendation.Item;
        var path = Path.GetFullPath(item.FullPath);
        if (Path.TrimEndingDirectorySeparator(path).Equals(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path)!), StringComparison.OrdinalIgnoreCase))
            throw new IOException("A drive root cannot be cleaned up.");
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            ((attributes & FileAttributes.Directory) != 0) != item.IsDirectory)
            throw new IOException("The item type changed since the scan. Scan again.");
        if (item.IsDirectory)
        {
            if (recommendation.Category != CleanupCategory.EmptyFolders || !item.IsScanComplete ||
                Directory.EnumerateFileSystemEntries(path).Any())
                throw new IOException("The directory is no longer confirmed empty. Scan again.");
        }
        else
        {
            var info = new FileInfo(path);
            if (info.Length != item.SizeBytes || info.LastWriteTime != item.LastModified)
                throw new IOException("The file changed since the scan. Scan again.");
        }
        if (recommendation.Category != CleanupCategory.DuplicateFiles) return EmptyLease.Instance;
        if (item.IsDirectory || recommendation.DuplicateSha256.Length != 64)
            throw new IOException("Full duplicate evidence is unavailable. Scan for duplicates again.");

        using var candidate = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var candidateHash = Convert.ToHexString(SHA256.HashData(candidate));
        if (!candidateHash.Equals(recommendation.DuplicateSha256, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The duplicate contents changed since the scan. Scan again.");

        selected ??= [recommendation];
        foreach (var evidence in recommendation.DuplicateFiles)
        {
            var survivorPath = Path.GetFullPath(evidence.FullPath);
            if (survivorPath.Equals(path, StringComparison.OrdinalIgnoreCase) || selected.Any(r => Contains(r.Item, survivorPath))) continue;
            FileStream? survivor = null;
            try
            {
                var info = new FileInfo(survivorPath);
                if (!info.Exists || (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 ||
                    info.Length != evidence.SizeBytes || info.LastWriteTime != evidence.LastModified) continue;
                // FileShare.Read prevents a second cleanup or external writer from changing this survivor
                // while the caller removes the candidate. Do not close it until deletion completes.
                survivor = new FileStream(survivorPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (survivor.Length == candidate.Length && Convert.ToHexString(SHA256.HashData(survivor)).Equals(candidateHash, StringComparison.Ordinal))
                {
                    var lease = survivor; survivor = null; return lease;
                }
            }
            catch (IOException) { /* Try another surviving member; inaccessible copies are not evidence. */ }
            catch (UnauthorizedAccessException) { }
            finally { survivor?.Dispose(); }
        }
        throw new IOException("No unchanged duplicate copy will remain outside the selected deletion set. Deselect a surviving copy or scan again.");
    }

    private static bool Contains(StorageItem selected, string path)
    {
        var selectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selected.FullPath));
        return path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase) ||
            (selected.IsDirectory && path.StartsWith(selectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class EmptyLease : IDisposable
    {
        public static readonly EmptyLease Instance = new();
        public void Dispose() { }
    }
}
