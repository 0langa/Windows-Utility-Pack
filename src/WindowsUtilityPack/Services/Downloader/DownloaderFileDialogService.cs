using Microsoft.Win32;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Dialog wrapper for downloader import/diagnostic file workflows.</summary>
internal sealed class DownloaderFileDialogService : IDownloaderFileDialogService
{
    public string? PickImportListFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import links",
            Filter = "Text files (*.txt;*.list;*.urls)|*.txt;*.list;*.urls|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickCookieFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select cookie file",
            Filter = "Cookie files (*.txt;*.json)|*.txt;*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickDiagnosticsExportPath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export downloader diagnostics",
            FileName = $"downloader-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.log",
            Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
