using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.SystemInfoDashboard;

public class SystemInfoViewModel : ViewModelBase
{
    // P/Invoke for GlobalMemoryStatusEx
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private string _osName                = string.Empty;
    private string _osVersion             = string.Empty;
    private string _osBuild               = string.Empty;
    private string _osDescription         = string.Empty;
    private string _architecture          = string.Empty;
    private string _processArchitecture   = string.Empty;
    private string _computerName          = string.Empty;
    private string _userName              = string.Empty;
    private string _cpuName               = string.Empty;
    private string _cpuCores              = string.Empty;
    private string _cpuLogicalProcessors  = string.Empty;
    private string _ramTotal              = string.Empty;
    private string _ramAvailable          = string.Empty;
    private string _gpuName               = string.Empty;
    private string _systemDrive           = string.Empty;
    private string _driveSummary          = string.Empty;
    private string _dotNetVersion         = string.Empty;
    private string _uptime                = string.Empty;
    private string _managedMemory         = string.Empty;
    private DateTime _snapshotUtc;
    private bool   _isLoading;

    public string OsName               { get => _osName;               private set => SetProperty(ref _osName, value); }
    public string OsVersion            { get => _osVersion;            private set => SetProperty(ref _osVersion, value); }
    public string OsBuild              { get => _osBuild;              private set => SetProperty(ref _osBuild, value); }
    public string OsDescription        { get => _osDescription;        private set => SetProperty(ref _osDescription, value); }
    public string Architecture         { get => _architecture;         private set => SetProperty(ref _architecture, value); }
    public string ProcessArchitecture  { get => _processArchitecture;  private set => SetProperty(ref _processArchitecture, value); }
    public string ComputerName         { get => _computerName;         private set => SetProperty(ref _computerName, value); }
    public string UserName             { get => _userName;             private set => SetProperty(ref _userName, value); }
    public string CpuName              { get => _cpuName;              private set => SetProperty(ref _cpuName, value); }
    public string CpuCores             { get => _cpuCores;             private set => SetProperty(ref _cpuCores, value); }
    public string CpuLogicalProcessors { get => _cpuLogicalProcessors; private set => SetProperty(ref _cpuLogicalProcessors, value); }
    public string RamTotal             { get => _ramTotal;             private set => SetProperty(ref _ramTotal, value); }
    public string RamAvailable         { get => _ramAvailable;         private set => SetProperty(ref _ramAvailable, value); }
    public string GpuName              { get => _gpuName;              private set => SetProperty(ref _gpuName, value); }
    public string SystemDrive          { get => _systemDrive;          private set => SetProperty(ref _systemDrive, value); }
    public string DriveSummary         { get => _driveSummary;         private set => SetProperty(ref _driveSummary, value); }
    public string DotNetVersion        { get => _dotNetVersion;        private set => SetProperty(ref _dotNetVersion, value); }
    public string Uptime               { get => _uptime;               private set => SetProperty(ref _uptime, value); }
    public string ManagedMemory        { get => _managedMemory;        private set => SetProperty(ref _managedMemory, value); }
    public DateTime SnapshotUtc        { get => _snapshotUtc;          private set => SetProperty(ref _snapshotUtc, value); }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public AsyncRelayCommand LoadCommand    { get; }
    public RelayCommand      CopyAllCommand { get; }
    public RelayCommand      CopyDiagnosticsCommand { get; }
    public AsyncRelayCommand ExportJsonCommand { get; }

    private readonly IClipboardService _clipboard;
    private readonly ISystemInfoReportService _reports;
    private SystemInfoSnapshot? _latestSnapshot;

    public SystemInfoViewModel(IClipboardService clipboard, ISystemInfoReportService reports)
    {
        _clipboard     = clipboard;
        _reports       = reports;
        LoadCommand    = new AsyncRelayCommand(LoadInfoAsync);
        CopyAllCommand = new RelayCommand(CopyAll);
        CopyDiagnosticsCommand = new RelayCommand(CopyAll);
        ExportJsonCommand = new AsyncRelayCommand(_ => ExportJsonAsync());

        LoadCommand.Execute(null);
    }

    private async Task LoadInfoAsync()
    {
        IsLoading = true;
        try
        {
            await Task.Run(() =>
            {
                var osVer  = Environment.OSVersion;
                var cpu    = ReadCpuNameFromRegistry();
                var memory = ReadMemory();
                var gpu    = ReadGpuNameFromRegistry();
                var drives = BuildDriveSummary();
                var uptime = FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64));
                var managedMemory = FormatBytes((ulong)GC.GetTotalMemory(forceFullCollection: false));
                var snapshotUtc = DateTime.UtcNow;

                var snapshot = new SystemInfoSnapshot
                {
                    OsName = "Windows",
                    OsVersion = osVer.Version.ToString(3),
                    OsBuild = osVer.Version.Build.ToString(),
                    OsDescription = RuntimeInformation.OSDescription,
                    Architecture = RuntimeInformation.OSArchitecture.ToString(),
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    ComputerName = Environment.MachineName,
                    UserName = Environment.UserName,
                    CpuName = cpu,
                    CpuCores = Environment.ProcessorCount.ToString(),
                    CpuLogicalProcessors = Environment.ProcessorCount.ToString(),
                    RamTotal = FormatBytes(memory.total),
                    RamAvailable = FormatBytes(memory.available),
                    GpuName = gpu,
                    SystemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:",
                    DriveSummary = drives,
                    DotNetVersion = RuntimeInformation.FrameworkDescription,
                    Uptime = uptime,
                    ManagedMemory = managedMemory,
                    SnapshotUtc = snapshotUtc,
                };

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _latestSnapshot      = snapshot;
                    OsName               = snapshot.OsName;
                    OsVersion            = snapshot.OsVersion;
                    OsBuild              = snapshot.OsBuild;
                    OsDescription        = snapshot.OsDescription;
                    Architecture         = snapshot.Architecture;
                    ProcessArchitecture  = snapshot.ProcessArchitecture;
                    ComputerName         = snapshot.ComputerName;
                    UserName             = snapshot.UserName;
                    CpuName              = snapshot.CpuName;
                    CpuCores             = snapshot.CpuCores;
                    CpuLogicalProcessors = snapshot.CpuLogicalProcessors;
                    RamTotal             = snapshot.RamTotal;
                    RamAvailable         = snapshot.RamAvailable;
                    GpuName              = snapshot.GpuName;
                    SystemDrive          = snapshot.SystemDrive;
                    DriveSummary         = snapshot.DriveSummary;
                    DotNetVersion        = snapshot.DotNetVersion;
                    Uptime               = snapshot.Uptime;
                    ManagedMemory        = snapshot.ManagedMemory;
                    SnapshotUtc          = snapshot.SnapshotUtc;
                });
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string ReadCpuNameFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string ReadGpuNameFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Video");
            if (key == null) return "Unknown";

            foreach (var sub in key.GetSubKeyNames())
            {
                using var videoKey = key.OpenSubKey(sub + @"\0000");
                var desc = videoKey?.GetValue("Device Description")?.ToString();
                if (!string.IsNullOrWhiteSpace(desc))
                    return desc;
                var drv = videoKey?.GetValue("DriverDesc")?.ToString();
                if (!string.IsNullOrWhiteSpace(drv))
                    return drv;
            }
        }
        catch { /* ignore */ }

        return "Unknown";
    }

    private static (ulong total, ulong available) ReadMemory()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref mem))
            return (mem.ullTotalPhys, mem.ullAvailPhys);

        // Fallback
        var info = GC.GetGCMemoryInfo();
        return ((ulong)info.TotalAvailableMemoryBytes, (ulong)info.TotalAvailableMemoryBytes);
    }

    private static string FormatBytes(ulong bytes)
    {
        const double gb = 1024.0 * 1024 * 1024;
        return $"{bytes / gb:F2} GB";
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
    }

    private static string BuildDriveSummary()
    {
        try
        {
            var lines = new List<string>();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var freeGb = drive.AvailableFreeSpace / (1024d * 1024d * 1024d);
                var totalGb = drive.TotalSize / (1024d * 1024d * 1024d);
                lines.Add($"{drive.Name} {drive.DriveFormat} {freeGb:F1}/{totalGb:F1} GB free");
            }

            return lines.Count > 0 ? string.Join(" | ", lines) : "No ready drives detected.";
        }
        catch
        {
            return "Drive summary unavailable.";
        }
    }

    private void CopyAll()
    {
        var snapshot = _latestSnapshot ?? BuildCurrentSnapshot();
        _clipboard.SetText(_reports.BuildTextReport(snapshot));
    }

    private async Task ExportJsonAsync()
    {
        var snapshot = _latestSnapshot ?? BuildCurrentSnapshot();
        var dialog = new SaveFileDialog
        {
            Title = "Export system diagnostics",
            FileName = $"system-diagnostics-{DateTime.Now:yyyyMMdd-HHmm}.json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await File.WriteAllTextAsync(dialog.FileName, _reports.BuildJsonReport(snapshot));
    }

    private SystemInfoSnapshot BuildCurrentSnapshot()
    {
        return new SystemInfoSnapshot
        {
            OsName = OsName,
            OsVersion = OsVersion,
            OsBuild = OsBuild,
            OsDescription = OsDescription,
            Architecture = Architecture,
            ProcessArchitecture = ProcessArchitecture,
            ComputerName = ComputerName,
            UserName = UserName,
            CpuName = CpuName,
            CpuCores = CpuCores,
            CpuLogicalProcessors = CpuLogicalProcessors,
            RamTotal = RamTotal,
            RamAvailable = RamAvailable,
            GpuName = GpuName,
            SystemDrive = SystemDrive,
            DriveSummary = DriveSummary,
            DotNetVersion = DotNetVersion,
            Uptime = Uptime,
            ManagedMemory = ManagedMemory,
            SnapshotUtc = SnapshotUtc,
        };
    }
}
