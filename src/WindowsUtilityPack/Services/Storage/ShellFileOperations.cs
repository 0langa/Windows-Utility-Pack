using System.IO;
using System.Runtime.InteropServices;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Performs file and directory deletion through the Windows Shell API
/// (<c>SHFileOperation</c>) instead of raw Win32 <c>DeleteFile</c> /
/// <c>RemoveDirectory</c>.
///
/// Why this matters:
/// <list type="bullet">
///   <item>
///     <description>
///       The Shell API is the same code path used by Windows Explorer.
///       Antivirus heuristic engines trust Shell-mediated operations and
///       are far less likely to flag them as suspicious compared to direct
///       file-system calls issued by a third-party process.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>SHFileOperation</c> handles both permanent deletion and
///       Recycle-Bin (undo) deletion uniformly through a single API,
///       which simplifies the calling code.
///     </description>
///   </item>
/// </list>
/// </summary>
internal static class ShellFileOperations
{
    // ── Win32 constants ──────────────────────────────────────────────────

    private const uint FO_DELETE = 0x0003;

    private const ushort FOF_SILENT           = 0x0004;  // No progress dialog
    private const ushort FOF_NOCONFIRMATION   = 0x0010;  // No "Are you sure?" prompt
    private const ushort FOF_ALLOWUNDO        = 0x0040;  // Send to Recycle Bin
    private const ushort FOF_NOERRORUI        = 0x0400;  // No error dialog on failure

    // ── P/Invoke ─────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public nint hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public nint hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>
    /// Deletes a file or directory through the Windows Shell.
    /// </summary>
    /// <param name="path">Full path to the file or directory.</param>
    /// <param name="recycle">
    ///   <see langword="true"/> to send to the Recycle Bin;
    ///   <see langword="false"/> for permanent deletion.
    /// </param>
    /// <exception cref="IOException">
    ///   Thrown when the Shell operation fails (includes the Shell error code).
    /// </exception>
    public static void Delete(string path, bool recycle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // SHFileOperation requires a double-null-terminated string.
        var fileOp = new SHFILEOPSTRUCT
        {
            wFunc  = FO_DELETE,
            pFrom  = path + '\0' + '\0',
            fFlags = (ushort)(FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
                              | (recycle ? FOF_ALLOWUNDO : 0)),
        };

        int result = SHFileOperation(ref fileOp);

        if (result != 0)
        {
            throw new IOException(
                $"Shell file operation failed for '{path}' (error 0x{result:X4}).");
        }
    }
}
