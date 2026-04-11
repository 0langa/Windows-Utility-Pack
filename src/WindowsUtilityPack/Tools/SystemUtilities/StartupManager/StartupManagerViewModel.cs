using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.StartupManager;

public class StartupEntry : ViewModelBase
{
    private string _name = string.Empty;
    private string _command = string.Empty;
    private bool   _isEnabled;
    private string _source = string.Empty;

    public string Name      { get => _name;      set => SetProperty(ref _name, value); }
    public string Command   { get => _command;   set => SetProperty(ref _command, value); }
    public bool   IsEnabled { get => _isEnabled;  set => SetProperty(ref _isEnabled, value); }
    public string Source    { get => _source;    set => SetProperty(ref _source, value); }
}

public class StartupManagerViewModel : ViewModelBase
{
    private const string RunKeyPath         = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunDisabledKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run_Disabled";

    private ObservableCollection<StartupEntry> _entries = [];
    private StartupEntry? _selectedEntry;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

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

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand LoadCommand { get; }
    public RelayCommand ToggleCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public StartupManagerViewModel()
    {
        LoadCommand    = new AsyncRelayCommand(LoadEntriesAsync);
        ToggleCommand  = new RelayCommand(ToggleSelected, () => SelectedEntry != null);
        RemoveCommand  = new RelayCommand(RemoveSelected, () => SelectedEntry != null);
        RefreshCommand = new RelayCommand(() => LoadCommand.Execute(null));

        LoadCommand.Execute(null);
    }

    private async Task LoadEntriesAsync()
    {
        IsLoading     = true;
        StatusMessage = "Loading startup entries…";

        try
        {
            var entries = await Task.Run(CollectEntries);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Entries.Clear();
                foreach (var e in entries)
                    Entries.Add(e);
            });

            StatusMessage = $"Loaded {Entries.Count} startup entries.";
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

    private static List<StartupEntry> CollectEntries()
    {
        var result = new List<StartupEntry>();

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
                        Source    = "HKCU"
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
                        Source    = "HKCU"
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
                        Source    = "HKLM"
                    });
                }
            }
        }
        catch
        {
            // HKLM may require elevation — silently skip
        }

        return result;
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
                    StatusMessage   = $"Disabled '{entry.Name}'.";
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
                    StatusMessage   = $"Enabled '{entry.Name}'.";
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
            StatusMessage = $"Removed '{entry.Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Remove failed: {ex.Message}";
        }
    }
}
