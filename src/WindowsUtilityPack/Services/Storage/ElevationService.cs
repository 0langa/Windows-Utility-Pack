using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Windows implementation of the elevation/admin mode service.
/// Uses WindowsPrincipal to check current privileges and ProcessStartInfo
/// with Verb="runas" to request elevation via UAC.
/// </summary>
public class ElevationService : IElevationService
{
    /// <inheritdoc/>
    public bool IsElevated { get; } = CheckIsElevated();

    /// <inheritdoc/>
    public async Task<bool> RestartElevatedAsync()
    {
        if (IsElevated) return true; // Already elevated — nothing to do.

        try
        {
            var processPath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrEmpty(processPath))
                return false;

            var startInfo = new ProcessStartInfo
            {
                FileName         = processPath,
                UseShellExecute  = true,
                Verb             = "runas", // Triggers UAC prompt
            };

            Process.Start(startInfo);

            // Shut down current (non-elevated) instance on the UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
                Application.Current.Shutdown());

            return true;
        }
        catch (Exception ex) when (
            ex is System.ComponentModel.Win32Exception ||
            ex is InvalidOperationException)
        {
            // User declined the UAC prompt (Win32Exception with NativeErrorCode 1223)
            return false;
        }
    }

    private static bool CheckIsElevated()
    {
        try
        {
            using var identity  = WindowsIdentity.GetCurrent();
            var       principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
