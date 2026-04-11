using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Polls lightweight system vitals (CPU, RAM, disk, network) on a background timer
/// and exposes the latest values for the homepage vitals bar.
/// All events are raised on the UI dispatcher thread.
/// </summary>
public sealed class SystemVitalsService : IDisposable
{
    // ── P/Invoke for total physical memory ────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // ── State ─────────────────────────────────────────────────────────────

    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _availMemCounter;
    private readonly float _totalRamGb;
    private readonly string _sysDrivePath;
    private long _prevBytesReceived;
    private long _prevBytesSent;
    private bool _networkBaselineSet;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    // ── Public properties ─────────────────────────────────────────────────

    /// <summary>Processor utilisation 0–100 (–1 when unavailable).</summary>
    public float CpuPercent { get; private set; } = -1;

    /// <summary>Percentage of RAM in use (–1 when unavailable).</summary>
    public float RamUsedPercent { get; private set; } = -1;

    /// <summary>Available RAM in GiB (–1 when unavailable).</summary>
    public float RamFreeGb { get; private set; } = -1;

    /// <summary>Total installed RAM in GiB (0 when unavailable).</summary>
    public float TotalRamGb => _totalRamGb;

    /// <summary>Free space on the system drive in GiB (–1 when unavailable).</summary>
    public double DiskFreeGb { get; private set; } = -1;

    /// <summary>Total size of the system drive in GiB (0 when unavailable).</summary>
    public double DiskTotalGb { get; private set; }

    /// <summary>Network download speed in KB/s (–1 when unavailable).</summary>
    public double NetworkReceiveKbps { get; private set; } = -1;

    /// <summary>Network upload speed in KB/s (–1 when unavailable).</summary>
    public double NetworkSendKbps { get; private set; } = -1;

    /// <summary>Raised on the UI thread after each poll cycle.</summary>
    public event EventHandler? Updated;

    // ── Constructor ───────────────────────────────────────────────────────

    public SystemVitalsService()
    {
        // CPU counter — first NextValue() always returns 0; discard it now.
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
            _cpuCounter.NextValue();
        }
        catch { _cpuCounter = null; }

        // Available memory counter.
        try
        {
            _availMemCounter = new PerformanceCounter("Memory", "Available MBytes", readOnly: true);
        }
        catch { _availMemCounter = null; }

        // Total RAM via kernel32.
        _totalRamGb = ReadTotalRamGb();

        // System drive initial disk read.
        _sysDrivePath = Path.GetPathRoot(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
        ReadDisk();

        // Network baseline.
        ReadNetworkBaseline();

        // Poll every 3 seconds on the background dispatcher priority.
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    // ── Timer callback ────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed) return;

        ReadCpu();
        ReadRam();
        ReadDisk();
        ReadNetwork();

        Updated?.Invoke(this, EventArgs.Empty);
    }

    // ── Readers ───────────────────────────────────────────────────────────

    private void ReadCpu()
    {
        try
        {
            if (_cpuCounter != null)
                CpuPercent = Math.Clamp(_cpuCounter.NextValue(), 0f, 100f);
        }
        catch { CpuPercent = -1; }
    }

    private void ReadRam()
    {
        try
        {
            if (_availMemCounter != null)
            {
                var freeMb = _availMemCounter.NextValue();
                RamFreeGb = freeMb / 1024f;
                RamUsedPercent = _totalRamGb > 0
                    ? Math.Clamp(100f * (1f - RamFreeGb / _totalRamGb), 0f, 100f)
                    : -1;
            }
        }
        catch { RamFreeGb = -1; RamUsedPercent = -1; }
    }

    private void ReadDisk()
    {
        try
        {
            var di = new DriveInfo(_sysDrivePath);
            DiskFreeGb = di.AvailableFreeSpace / 1_073_741_824.0;
            DiskTotalGb = di.TotalSize / 1_073_741_824.0;
        }
        catch { DiskFreeGb = -1; }
    }

    private void ReadNetworkBaseline()
    {
        try
        {
            GetNetworkBytes(out _prevBytesReceived, out _prevBytesSent);
            _networkBaselineSet = true;
        }
        catch { }
    }

    private void ReadNetwork()
    {
        if (!_networkBaselineSet) { ReadNetworkBaseline(); return; }
        try
        {
            GetNetworkBytes(out var received, out var sent);
            var intervalSec = _timer.Interval.TotalSeconds;
            NetworkReceiveKbps = Math.Max(0, (received - _prevBytesReceived) / 1024.0 / intervalSec);
            NetworkSendKbps = Math.Max(0, (sent - _prevBytesSent) / 1024.0 / intervalSec);
            _prevBytesReceived = received;
            _prevBytesSent = sent;
        }
        catch { NetworkReceiveKbps = -1; NetworkSendKbps = -1; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static float ReadTotalRamGb()
    {
        try
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref status))
                return status.ullTotalPhys / 1_073_741_824.0f;
        }
        catch { }
        return 0;
    }

    private static void GetNetworkBytes(out long received, out long sent)
    {
        received = 0; sent = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel) continue;
            var stats = nic.GetIPv4Statistics();
            received += stats.BytesReceived;
            sent += stats.BytesSent;
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _cpuCounter?.Dispose();
        _availMemCounter?.Dispose();
    }
}
