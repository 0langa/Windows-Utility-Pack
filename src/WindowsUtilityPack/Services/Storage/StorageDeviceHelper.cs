using System.IO;
using System.Runtime.InteropServices;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Windows P/Invoke helper for storage device property queries.
/// Used by DriveAnalysisService to detect SSD vs HDD.
///
/// Based on IOCTL_STORAGE_QUERY_PROPERTY / StorageDeviceSeekPenaltyProperty.
/// A seek penalty indicates a spinning HDD; no seek penalty indicates SSD.
/// </summary>
internal static class StorageDeviceHelper
{
    private const uint IOCTL_STORAGE_QUERY_PROPERTY      = 0x002D1400;
    private const int  StorageDeviceSeekPenaltyProperty  = 7;

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public int PropertyId;
        public int QueryType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.Bool)]
        public bool IncursSeekPenalty;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
        string lpFileName,
        uint   dwDesiredAccess,
        uint   dwShareMode,
        IntPtr lpSecurityAttributes,
        uint   dwCreationDisposition,
        uint   dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        Microsoft.Win32.SafeHandles.SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref STORAGE_PROPERTY_QUERY lpInBuffer,
        uint nInBufferSize,
        ref DEVICE_SEEK_PENALTY_DESCRIPTOR lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    private const uint GENERIC_READ          = 0x80000000;
    private const uint FILE_SHARE_READ_WRITE = 0x00000003;
    private const uint OPEN_EXISTING         = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    /// <summary>
    /// Returns <c>true</c> if the drive at <paramref name="rootPath"/> has rotational
    /// media (HDD). Throws on access denied or unsupported platforms.
    /// </summary>
    public static bool IsRotational(string rootPath)
    {
        var driveLetter = Path.GetPathRoot(rootPath)?.TrimEnd('\\') ?? rootPath.TrimEnd('\\');
        var devicePath  = $@"\\.\{driveLetter}";

        using var handle = CreateFile(
            devicePath,
            0,                       // query-only — no read/write access needed
            FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (handle.IsInvalid)
            throw new IOException($"Cannot open device {devicePath}");

        var query = new STORAGE_PROPERTY_QUERY
        {
            PropertyId           = StorageDeviceSeekPenaltyProperty,
            QueryType            = 0,  // PropertyStandardQuery
            AdditionalParameters = [0],
        };

        var descriptor = new DEVICE_SEEK_PENALTY_DESCRIPTOR();
        bool ok = DeviceIoControl(
            handle,
            IOCTL_STORAGE_QUERY_PROPERTY,
            ref query,
            (uint)Marshal.SizeOf(query),
            ref descriptor,
            (uint)Marshal.SizeOf(descriptor),
            out _,
            IntPtr.Zero);

        if (!ok)
            throw new IOException("DeviceIoControl failed for seek-penalty query.");

        return descriptor.IncursSeekPenalty;
    }

    /// <summary>
    /// Returns a human-readable media-type string for the given drive root (e.g. "C:\").
    /// Uses the <see cref="DriveType"/> BCL enum as a reliable cross-context fallback.
    /// </summary>
    public static string GetMediaType(string driveName)
    {
        if (string.IsNullOrWhiteSpace(driveName))
            return "Unknown";

        // Try rotational detection first (Windows only, may fail without elevation)
        try
        {
            return IsRotational(driveName) ? "HDD" : "SSD";
        }
        catch
        {
            // Graceful degradation to DriveType-based string
            return GetMediaTypeFallback(driveName);
        }
    }

    private static string GetMediaTypeFallback(string driveName)
    {
        try
        {
            var di = new DriveInfo(driveName);
            return di.DriveType switch
            {
                DriveType.Fixed            => "Fixed Disk",
                DriveType.Removable        => "Removable",
                DriveType.Network          => "Network",
                DriveType.CDRom            => "Optical",
                DriveType.Ram              => "RAM Disk",
                DriveType.NoRootDirectory  => "No Root",
                _                          => "Unknown"
            };
        }
        catch
        {
            return "Unknown";
        }
    }
}
