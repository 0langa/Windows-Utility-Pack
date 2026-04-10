using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
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
    private string _architecture          = string.Empty;
    private string _computerName          = string.Empty;
    private string _userName              = string.Empty;
    private string _cpuName               = string.Empty;
    private string _cpuCores              = string.Empty;
    private string _cpuLogicalProcessors  = string.Empty;
    private string _ramTotal              = string.Empty;
    private string _ramAvailable          = string.Empty;
    private string _gpuName               = string.Empty;
    private string _systemDrive           = string.Empty;
    private string _dotNetVersion         = string.Empty;
    private bool   _isLoading;

    public string OsName               { get => _osName;               private set => SetProperty(ref _osName, value); }
    public string OsVersion            { get => _osVersion;            private set => SetProperty(ref _osVersion, value); }
    public string OsBuild              { get => _osBuild;              private set => SetProperty(ref _osBuild, value); }
    public string Architecture         { get => _architecture;         private set => SetProperty(ref _architecture, value); }
    public string ComputerName         { get => _computerName;         private set => SetProperty(ref _computerName, value); }
    public string UserName             { get => _userName;             private set => SetProperty(ref _userName, value); }
    public string CpuName              { get => _cpuName;              private set => SetProperty(ref _cpuName, value); }
    public string CpuCores             { get => _cpuCores;             private set => SetProperty(ref _cpuCores, value); }
    public string CpuLogicalProcessors { get => _cpuLogicalProcessors; private set => SetProperty(ref _cpuLogicalProcessors, value); }
    public string RamTotal             { get => _ramTotal;             private set => SetProperty(ref _ramTotal, value); }
    public string RamAvailable         { get => _ramAvailable;         private set => SetProperty(ref _ramAvailable, value); }
    public string GpuName              { get => _gpuName;              private set => SetProperty(ref _gpuName, value); }
    public string SystemDrive          { get => _systemDrive;          private set => SetProperty(ref _systemDrive, value); }
    public string DotNetVersion        { get => _dotNetVersion;        private set => SetProperty(ref _dotNetVersion, value); }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public AsyncRelayCommand LoadCommand    { get; }
    public RelayCommand      CopyAllCommand { get; }

    private readonly IClipboardService _clipboard;

    public SystemInfoViewModel(IClipboardService clipboard)
    {
        _clipboard     = clipboard;
        LoadCommand    = new AsyncRelayCommand(LoadInfoAsync);
        CopyAllCommand = new RelayCommand(CopyAll);

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

                Application.Current.Dispatcher.Invoke(() =>
                {
                    OsName               = "Windows";
                    OsVersion            = osVer.Version.ToString(3);
                    OsBuild              = osVer.Version.Build.ToString();
                    Architecture         = RuntimeInformation.OSArchitecture.ToString();
                    ComputerName         = Environment.MachineName;
                    UserName             = Environment.UserName;
                    CpuName              = cpu;
                    CpuCores             = Environment.ProcessorCount.ToString();
                    CpuLogicalProcessors = Environment.ProcessorCount.ToString();
                    RamTotal             = FormatBytes(memory.total);
                    RamAvailable         = FormatBytes(memory.available);
                    GpuName              = gpu;
                    SystemDrive          = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
                    DotNetVersion        = RuntimeInformation.FrameworkDescription;
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

    private void CopyAll()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== System Information ===");
        sb.AppendLine();
        sb.AppendLine("[OS]");
        sb.AppendLine($"  Name         : {OsName}");
        sb.AppendLine($"  Version      : {OsVersion}");
        sb.AppendLine($"  Build        : {OsBuild}");
        sb.AppendLine($"  Architecture : {Architecture}");
        sb.AppendLine();
        sb.AppendLine("[Computer]");
        sb.AppendLine($"  Name         : {ComputerName}");
        sb.AppendLine($"  User         : {UserName}");
        sb.AppendLine($"  System Drive : {SystemDrive}");
        sb.AppendLine();
        sb.AppendLine("[CPU]");
        sb.AppendLine($"  Name              : {CpuName}");
        sb.AppendLine($"  Cores             : {CpuCores}");
        sb.AppendLine($"  Logical Processors: {CpuLogicalProcessors}");
        sb.AppendLine();
        sb.AppendLine("[Memory]");
        sb.AppendLine($"  Total     : {RamTotal}");
        sb.AppendLine($"  Available : {RamAvailable}");
        sb.AppendLine();
        sb.AppendLine("[Other]");
        sb.AppendLine($"  .NET Version : {DotNetVersion}");
        sb.AppendLine($"  GPU          : {GpuName}");

        _clipboard.SetText(sb.ToString());
    }
}
