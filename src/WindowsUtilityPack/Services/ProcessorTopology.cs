using System.Runtime.InteropServices;

namespace WindowsUtilityPack.Services;

internal static class ProcessorTopology
{
    public static int? PhysicalCoreCount()
    {
        uint length = 0;
        _ = GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref length);
        if (Marshal.GetLastWin32Error() != 122 || length is 0 or > 16 * 1024 * 1024) return null;
        var buffer = Marshal.AllocHGlobal(checked((int)length));
        try
        {
            if (!GetLogicalProcessorInformationEx(0, buffer, ref length)) return null;
            var count = 0; var offset = 0;
            while (offset < length)
            {
                if (length - offset < 8) return null;
                var size = Marshal.ReadInt32(buffer, offset + 4);
                if (size < 8 || size > length - offset) return null;
                if (Marshal.ReadInt32(buffer, offset) == 0) count++;
                offset += size;
            }
            return count > 0 ? count : null;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(int relationship, IntPtr buffer, ref uint returnedLength);
}
