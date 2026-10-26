using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace WindowsUtilityPack.Services.FileTools;

public static class FileOverwriteService
{
    /// <summary>Overwrites the opened file and deletes that same file. This is not physical-media sanitization.</summary>
    public static void OverwriteAndDelete(string path, int passes, CancellationToken cancellationToken = default)
    {
        if (passes is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(passes), "Choose 1 to 7 passes.");
        path = Path.GetFullPath(path);
        if (!BatchRenameService.IsValidFileName(Path.GetFileName(path))) throw new IOException("Select a regular file.");
        cancellationToken.ThrowIfCancellationRequested();
        // Open the reparse point itself, never its target. Exclusive sharing pins the file
        // throughout validation, overwriting, and deletion (no path-based delete race).
        using var handle = CreateFileW(path, 0x40000000 | 0x00010000, 0, 0, 3, 0x00200000, 0);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            if (error is 2 or 3) throw new FileNotFoundException("The file no longer exists.", path);
            throw new IOException($"Cannot open {path}: {new Win32Exception(error).Message}");
        }
        if (!GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 || info.NumberOfLinks != 1)
            throw new IOException("Linked files and directories cannot be shredded.");

        using var stream = new FileStream(handle, FileAccess.Write);
        var buffer = new byte[65536];
        for (var pass = 0; pass < passes; pass++)
        {
            stream.Position = 0;
            var remaining = stream.Length;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min(buffer.Length, remaining);
                RandomNumberGenerator.Fill(buffer.AsSpan(0, count));
                stream.Write(buffer, 0, count);
                remaining -= count;
            }
            stream.Flush(flushToDisk: true);
        }
        cancellationToken.ThrowIfCancellationRequested();
        byte delete = 1; // FILE_DISPOSITION_INFO.DeleteFile is a one-byte BOOLEAN.
        if (!SetFileInformationByHandle(handle, 4, ref delete, 1)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public FileAttributes Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint VolumeSerialNumber, SizeHigh, SizeLow, NumberOfLinks, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, ref byte information, uint size);
}
