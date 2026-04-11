using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.StartupManager;

public class StartupEntry : ViewModelBase
{
    private string _name = string.Empty;
    private string _command = string.Empty;
    private bool   _isEnabled;
    private string _source = string.Empty;
    private string _executablePath = string.Empty;
    private bool _targetExists;
    private bool _isPotentiallyRisky;

    public string Name      { get => _name;      set => SetProperty(ref _name, value); }
    public string Command   { get => _command;   set => SetProperty(ref _command, value); }
    public bool   IsEnabled { get => _isEnabled;  set => SetProperty(ref _isEnabled, value); }
    public string Source    { get => _source;    set => SetProperty(ref _source, value); }
    public string ExecutablePath { get => _executablePath; set => SetProperty(ref _executablePath, value); }
    public bool TargetExists { get => _targetExists; set => SetProperty(ref _targetExists, value); }
    public bool IsPotentiallyRisky { get => _isPotentiallyRisky; set => SetProperty(ref _isPotentiallyRisky, value); }
}

public class StartupManagerViewModel : ViewModelBase
{
    private const string RunKeyPath         = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunDisabledKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run_Disabled";

    private ObservableCollection<StartupEntry> _entries = [];
    private StartupEntry? _selectedEntry;
    private bool _isLoading;
    private bool _hklmEntriesSkipped;
    private string _statusMessage = string.Empty;
    private string _entryDiagnosticsSummary = string.Empty;

    private readonly IClipboardService _clipboard;
    private readonly IStartupDiagnosticsService _diagnostics;

    public ObservableCollection<StartupEntry> Entries
    {
        get => _entries;
        private set => SetProperty(ref _entries, value);
    }

    public StartupEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HklmEntriesSkipped
    {
        get => _hklmEntriesSkipped;
        private set => SetProperty(ref _hklmEntriesSkipped, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string EntryDiagnosticsSummary
    {
        get => _entryDiagnosticsSummary;
        private set => SetProperty(ref _entryDiagnosticsSummary, value);
    }

    public AsyncRelayCommand LoadCommand { get; }
    public RelayCommand ToggleCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand CopyDiagnosticsCommand { get; }

    public StartupManagerViewModel(IClipboardService clipboard, IStartupDiagnosticsService diagnostics)
    {
        _clipboard = clipboard;
        _diagnostics = diagnostics;

        LoadCommand    = new AsyncRelayCommand(LoadEntriesAsync);
        ToggleCommand  = new RelayCommand(ToggleSelected, () => SelectedEntry != null);
        RemoveCommand  = new RelayCommand(RemoveSelected, () => SelectedEntry != null);
        RefreshCommand = new RelayCommand(() => LoadCommand.Execute(null));
        ExportCsvCommand = new RelayCommand(ExportCsv, () => Entries.Count > 0);
        CopyDiagnosticsCommand = new RelayCommand(CopyDiagnostics, () => Entries.Count > 0);

        LoadCommand.Execute(null);
    }

    private async Task LoadEntriesAsync()
    {
        IsLoading     = true;
        HklmEntriesSkipped = false;
        StatusMessage = "Loading startup entries…";

        try
        {
            var snapshot = await Task.Run(CollectEntries);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Entries.Clear();
                foreach (var e in snapshot.Entries)
                    Entries.Add(e);
            });

            HklmEntriesSkipped = snapshot.HklmSkipped;
            RefreshDiagnosticsSummary("Loaded");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading entries: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static StartupCollectionResult CollectEntries()
    {
        var result = new List<StartupEntry>();
        var hklmSkipped = false;

        // HKCU enabled
        using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
        {
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    result.Add(new StartupEntry
                    {
                        Name      = name,
                        Command   = key.GetValue(name)?.ToString() ?? string.Empty,
                        IsEnabled = true,
                        Source    = "HKCU",
                    });
                }
            }
        }

        // HKCU disabled
        using (var key = Registry.CurrentUser.OpenSubKey(RunDisabledKeyPath))
        {
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    result.Add(new StartupEntry
                    {
                        Name      = name,
                        Command   = key.GetValue(name)?.ToString() ?? string.Empty,
                        IsEnabled = false,
                        Source    = "HKCU",
                    });
                }
            }
        }

        // HKLM enabled (read-only, just list)
        try
        {
            using var lmKey = Registry.LocalMachine.OpenSubKey(RunKeyPath);
            if (lmKey != null)
            {
                foreach (var name in lmKey.GetValueNames())
                {
                    result.Add(new StartupEntry
                    {
                        Name      = name,
                        Command   = lmKey.GetValue(name)?.ToString() ?? string.Empty,
                        IsEnabled = true,
                        Source    = "HKLM",
                    });
                }
            }
        }
        catch
        {
            hklmSkipped = true;
        }

        foreach (var entry in result)
        {
            var path = ExtractExecutablePath(entry.Command);
            entry.ExecutablePath = path;
            entry.TargetExists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            entry.IsPotentiallyRisky = IsPotentiallyRiskyCommand(entry.Command);
        }

        return new StartupCollectionResult(result, hklmSkipped);
    }

    private void ToggleSelected()
    {
        if (SelectedEntry is not { } entry)
            return;

        try
        {
            if (entry.Source != "HKCU")
            {
                StatusMessage = "Can only toggle HKCU entries (HKLM requires elevation).";
                return;
            }

            if (entry.IsEnabled)
            {
                // Move from Run → Run_Disabled
                using var src = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                using var dst = Registry.CurrentUser.CreateSubKey(RunDisabledKeyPath);
                if (src != null && dst != null)
                {
                    dst.SetValue(entry.Name, entry.Command);
                    src.DeleteValue(entry.Name, throwOnMissingValue: false);
                    entry.IsEnabled = false;
                    RefreshDiagnosticsSummary($"Disabled '{entry.Name}'.");
                }
            }
            else
            {
                // Move from Run_Disabled → Run
                using var src = Registry.CurrentUser.OpenSubKey(RunDisabledKeyPath, writable: true);
                using var dst = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                if (src != null && dst != null)
                {
                    dst.SetValue(entry.Name, entry.Command);
                    src.DeleteValue(entry.Name, throwOnMissingValue: false);
                    entry.IsEnabled = true;
                    RefreshDiagnosticsSummary($"Enabled '{entry.Name}'.");
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Toggle failed: {ex.Message}";
        }
    }

    private void RemoveSelected()
    {
        if (SelectedEntry is not { } entry)
            return;

        try
        {
            if (entry.Source != "HKCU")
            {
                StatusMessage = "Can only remove HKCU entries (HKLM requires elevation).";
                return;
            }

            var keyPath = entry.IsEnabled ? RunKeyPath : RunDisabledKeyPath;
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            key?.DeleteValue(entry.Name, throwOnMissingValue: false);

            Entries.Remove(entry);
            SelectedEntry = null;
            RefreshDiagnosticsSummary($"Removed '{entry.Name}'.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Remove failed: {ex.Message}";
        }
    }

    private void ExportCsv()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export startup entries",
                FileName = $"startup-entries-{DateTime.Now:yyyyMMdd-HHmm}.csv",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true,
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var csv = _diagnostics.BuildCsv(Entries.Select(ToDiagnostic).ToList());
            File.WriteAllText(dialog.FileName, csv, Encoding.UTF8);
            StatusMessage = $"Exported startup CSV to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"CSV export failed: {ex.Message}";
        }
    }

    private void CopyDiagnostics()
    {
        var report = _diagnostics.BuildDiagnosticsReport(Entries.Select(ToDiagnostic).ToList(), HklmEntriesSkipped);
        _clipboard.SetText(report);
        StatusMessage = "Startup diagnostics copied to clipboard.";
    }

    private void RefreshDiagnosticsSummary(string prefix)
    {
        var summary = _diagnostics.Summarize(Entries.Select(ToDiagnostic).ToList());
        EntryDiagnosticsSummary =
            $"Enabled {summary.EnabledEntries} | Disabled {summary.DisabledEntries} | " +
            $"Missing target {summary.MissingTargetEntries} | Risk flagged {summary.RiskFlaggedEntries}";
        StatusMessage = $"{prefix}: {summary.TotalEntries} entries.";
        RelayCommand.RaiseCanExecuteChanged();
    }

    private static StartupEntryDiagnostic ToDiagnostic(StartupEntry entry)
    {
        return new StartupEntryDiagnostic
        {
            Name = entry.Name,
            Command = entry.Command,
            IsEnabled = entry.IsEnabled,
            Source = entry.Source,
            ExecutablePath = entry.ExecutablePath,
            TargetExists = entry.TargetExists,
            IsPotentiallyRisky = entry.IsPotentiallyRisky,
        };
    }

    private static string ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return string.Empty;
        }

        var trimmed = Environment.ExpandEnvironmentVariables(command.Trim());
        string candidate;

        if (trimmed.StartsWith('"'))
        {
            var nextQuote = trimmed.IndexOf('"', 1);
            candidate = nextQuote > 1 ? trimmed[1..nextQuote] : string.Empty;
        }
        else
        {
            var firstSpace = trimmed.IndexOf(' ');
            candidate = firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
        }

        return candidate.Trim();
    }

    private static bool IsPotentiallyRiskyCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var normalized = command.ToLowerInvariant();
        return normalized.Contains("-enc")
            || normalized.Contains("frombase64string")
            || normalized.Contains(" wscript")
            || normalized.Contains(" cscript")
            || normalized.Contains("\\appdata\\local\\temp");
    }

    private sealed record StartupCollectionResult(List<StartupEntry> Entries, bool HklmSkipped);
}
