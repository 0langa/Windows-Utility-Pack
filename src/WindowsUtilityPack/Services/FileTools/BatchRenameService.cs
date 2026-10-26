using System.IO;

namespace WindowsUtilityPack.Services.FileTools;

/// <summary>Publishes a complete rename batch without overwriting files; rolls back failures.</summary>
public static class BatchRenameService
{
    public static bool IsValidFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." ||
            name.EndsWith('.') || name.EndsWith(' ') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;
        var stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return stem is not ("CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$") &&
               !(stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                 "123456789¹²³".Contains(stem[3]));
    }

    public static int Rename(string folder, IReadOnlyList<(string Source, string Target)> changes)
    {
        folder = Path.GetFullPath(folder);
        var entries = changes.Where(c => c.Source != c.Target).Select(c =>
        {
            if (!IsValidFileName(c.Source) || !IsValidFileName(c.Target))
                throw new IOException($"Invalid Windows file name: {c.Target}");
            return new Entry(Path.Combine(folder, c.Source), Path.Combine(folder, c.Target),
                Path.Combine(folder, ".wup-rename-" + Guid.NewGuid().ToString("N") + ".tmp"));
        }).ToArray();

        var sources = entries.Select(e => e.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sources.Count != entries.Length || entries.Select(e => e.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            throw new IOException("The rename batch contains duplicate sources or targets.");
        foreach (var e in entries)
        {
            var info = new FileInfo(e.Source);
            if (!info.Exists) throw new FileNotFoundException("A source file no longer exists.", e.Source);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException($"Linked files cannot be renamed in a batch: {e.Source}");
            if (Directory.Exists(e.Target) || (File.Exists(e.Target) && !sources.Contains(e.Target)))
                throw new IOException($"The destination already exists: {e.Target}");
        }

        try
        {
            foreach (var e in entries) { File.Move(e.Source, e.Staged); e.State = 1; }
            foreach (var e in entries) { File.Move(e.Staged, e.Target); e.State = 2; }
            return entries.Length;
        }
        catch (Exception error)
        {
            var recovery = new List<string>();
            // Re-stage published files first, so swaps and cycles can also be restored.
            foreach (var e in entries.Reverse().Where(e => e.State == 2))
            {
                try { File.Move(e.Target, e.Staged); e.State = 1; }
                catch (Exception ex) { recovery.Add($"{e.Target}: {ex.Message}"); }
            }
            foreach (var e in entries.Reverse().Where(e => e.State == 1))
            {
                try { File.Move(e.Staged, e.Source); e.State = 0; }
                catch (Exception ex) { recovery.Add($"{e.Staged} → {e.Source}: {ex.Message}"); }
            }
            throw new IOException(recovery.Count == 0
                ? $"No renames were kept. {error.Message}"
                : $"Rename failed. Some files require recovery:\n{string.Join("\n", recovery)}", error);
        }
    }

    private sealed class Entry(string source, string target, string staged)
    {
        public string Source { get; } = source;
        public string Target { get; } = target;
        public string Staged { get; } = staged;
        public int State { get; set; }
    }
}
