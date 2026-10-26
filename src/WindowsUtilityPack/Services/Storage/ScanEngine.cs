using System.Diagnostics;
using System.IO;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// High-performance storage scan engine.
///
/// Performance design:
///   - Enumerates the file system on a background thread (Task.Run).
///   - Uses Directory.EnumerateFileSystemEntries with per-level enumeration
///     to control recursion, enabling clean cancellation and progress reporting.
///   - Aggregates directory sizes bottom-up after the tree is built.
///   - Reports progress every N items to avoid UI thread flooding.
///   - Uses EnumerationOptions.IgnoreInaccessible to skip locked directories gracefully.
///
/// Future optimization path:
///   - The architecture supports swapping in an NTFS USN journal reader for
///     near-instant full-volume scans. The IScanEngine interface remains unchanged.
/// </summary>
public class ScanEngine : IScanEngine
{
    /// <inheritdoc/>
    public async Task<StorageItem> ScanAsync(
        string rootPath,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Scan root not found: {rootPath}");

        return await Task.Run(() =>
            ScanDirectory(rootPath, options, progress, cancellationToken),
            cancellationToken);
    }

    // ── Core recursive scan ───────────────────────────────────────────────────

    private static StorageItem ScanDirectory(
        string rootPath,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        // Counters tracked across recursive calls via a shared state object
        var state = new ScanState();

        var root = BuildTree(rootPath, null, 0, options, state, progress, stopwatch, ct);

        // Compute aggregated sizes bottom-up
        ComputeAggregates(root, ct);

        return root;
    }

    /// <summary>
    /// Recursively enumerates a single directory and builds the tree.
    /// Returns immediately with an empty children list if the directory is inaccessible.
    /// </summary>
    private static StorageItem BuildTree(
        string path,
        StorageItem? parent,
        int depth,
        ScanOptions options,
        ScanState state,
        IProgress<ScanProgress>? progress,
        Stopwatch stopwatch,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var dirInfo = new DirectoryInfo(path);
        var node = new StorageItem
        {
            FullPath     = dirInfo.FullName,
            Name         = dirInfo.Name,
            IsDirectory  = true,
            Attributes   = dirInfo.Attributes,
            LastModified = SafeGetDate(() => dirInfo.LastWriteTime),
            CreatedAt    = SafeGetDate(() => dirInfo.CreationTime),
            Depth        = depth,
            Parent       = parent,
        };

        state.DirsFound++;
        state.ItemsEnumerated++;

        // Skip hidden/system directories if not opted in
        if (!options.IncludeHidden && node.IsHidden) return node;
        if (!options.IncludeSystem && node.IsSystem) return node;
        if (options.MaxDepth > 0   && depth >= options.MaxDepth) return node;
        if ((node.Attributes & FileAttributes.ReparsePoint) != 0 || depth >= 256) return node;

        node.IsScanComplete = true;

        var enumOptions = new EnumerationOptions
        {
            IgnoreInaccessible   = false,
            RecurseSubdirectories = false,
            AttributesToSkip     = 0,
        };

        // ── Enumerate child files ─────────────────────────────────────────────
        try
        {
            foreach (var fileInfo in dirInfo.EnumerateFiles("*", enumOptions))
            {
                ct.ThrowIfCancellationRequested();

                if ((fileInfo.Attributes & BuildAttributeSkip(options)) != 0)
                {
                    node.IsScanComplete = false;
                    continue;
                }

                var fileNode = new StorageItem
                {
                    FullPath     = fileInfo.FullName,
                    Name         = fileInfo.Name,
                    IsDirectory  = false,
                    SizeBytes    = fileInfo.Length,
                    Attributes   = fileInfo.Attributes,
                    LastModified = fileInfo.LastWriteTime,
                    CreatedAt    = fileInfo.CreationTime,
                    Depth        = depth + 1,
                    Parent       = node,
                };

                fileNode.TotalSizeBytes      = fileNode.SizeBytes;
                fileNode.TotalAllocatedBytes = fileNode.AllocatedBytes;
                fileNode.FileCount           = 1;

                if (fileNode.SizeBytes >= options.MinFileSizeBytes)
                    node.Children.Add(fileNode);
                else
                    node.IsScanComplete = false;

                state.FilesFound++;
                state.ItemsEnumerated++;
                state.BytesCounted += fileNode.SizeBytes;

                MaybeReportProgress(state, path, progress, stopwatch, options.ProgressIntervalItems);
            }
        }
        catch (UnauthorizedAccessException) { node.IsScanComplete = false; }
        catch (IOException)                 { node.IsScanComplete = false; }

        // ── Enumerate child directories (recursion) ───────────────────────────
        try
        {
            foreach (var subDirInfo in dirInfo.EnumerateDirectories("*", enumOptions))
            {
                ct.ThrowIfCancellationRequested();

                var childNode = BuildTree(
                    subDirInfo.FullName, node, depth + 1, options, state, progress, stopwatch, ct);
                node.Children.Add(childNode);
                if (!childNode.IsScanComplete) node.IsScanComplete = false;
            }
        }
        catch (UnauthorizedAccessException) { node.IsScanComplete = false; }
        catch (IOException)                 { node.IsScanComplete = false; }

        return node;
    }

    /// <summary>
    /// Post-order bottom-up aggregation: fills TotalSizeBytes, FileCount, DirectoryCount
    /// for every directory node after the tree is fully built.
    /// </summary>
    private static void ComputeAggregates(StorageItem node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!node.IsDirectory)
        {
            node.TotalSizeBytes = node.SizeBytes;
            return;
        }

        long totalSize  = 0;
        long totalAllocated = 0;
        int  fileCount  = 0;
        int  dirCount   = 0;

        foreach (var child in node.Children)
        {
            ComputeAggregates(child, cancellationToken);
            totalSize += child.TotalSizeBytes;
            totalAllocated += child.TotalAllocatedBytes;
            fileCount += child.FileCount;
            dirCount  += child.DirectoryCount;

            if (child.IsDirectory) dirCount++;
        }

        node.TotalSizeBytes = totalSize;
        node.TotalAllocatedBytes = totalAllocated;
        node.FileCount      = fileCount;
        node.DirectoryCount = dirCount;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FileAttributes BuildAttributeSkip(ScanOptions options)
    {
        var skip = FileAttributes.ReparsePoint; // Always skip junctions/symlinks to avoid cycles
        if (!options.IncludeHidden) skip |= FileAttributes.Hidden;
        if (!options.IncludeSystem) skip |= FileAttributes.System;
        return skip;
    }

    private static void MaybeReportProgress(
        ScanState state,
        string currentPath,
        IProgress<ScanProgress>? progress,
        Stopwatch sw,
        int interval)
    {
        if (progress == null || state.ItemsEnumerated % Math.Max(1, interval) != 0) return;

        progress.Report(new ScanProgress
        {
            ItemsEnumerated = state.ItemsEnumerated,
            FilesFound      = state.FilesFound,
            DirsFound       = state.DirsFound,
            BytesCounted    = state.BytesCounted,
            CurrentPath     = currentPath,
            Elapsed         = sw.Elapsed,
        });
    }

    private static DateTime SafeGetDate(Func<DateTime> getter)
    {
        try { return getter(); } catch { return DateTime.MinValue; }
    }

    // ── Shared mutable scan state (only used on the background thread) ────────

    private sealed class ScanState
    {
        public int  ItemsEnumerated;
        public int  FilesFound;
        public int  DirsFound;
        public long BytesCounted;
    }
}
