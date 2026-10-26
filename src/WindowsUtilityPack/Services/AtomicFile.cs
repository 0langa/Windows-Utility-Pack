using System.IO;
using System.Text;

namespace WindowsUtilityPack.Services;

public static class AtomicFile
{
    public static void WriteAllText(string path, string content, Encoding? encoding = null, string? backupPath = null,
        CancellationToken cancellationToken = default)
        => Write(path, stream => { using var writer = new StreamWriter(stream, encoding ?? new UTF8Encoding(false), leaveOpen: true); writer.Write(content); }, backupPath, cancellationToken);

    public static void WriteAllBytes(string path, byte[] bytes, string? backupPath = null, CancellationToken cancellationToken = default)
        => Write(path, stream => stream.Write(bytes), backupPath, cancellationToken);

    private static void Write(string path, Action<FileStream> write, string? backupPath, CancellationToken cancellationToken)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, ".wup-write-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temporary, path, backupPath);
            else File.Move(temporary, path);
        }
        finally { File.Delete(temporary); }
    }

    public static Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default,
        Encoding? encoding = null, string? backupPath = null) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteAllText(path, content, encoding, backupPath, cancellationToken);
    }, cancellationToken);
}
