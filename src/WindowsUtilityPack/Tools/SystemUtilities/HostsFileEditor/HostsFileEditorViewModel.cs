using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.HostsFileEditor;

public class HostsEntry
{
    public string IpAddress { get; set; } = string.Empty;
    public string Hostname  { get; set; } = string.Empty;
    public string Comment   { get; set; } = string.Empty;
    public bool   IsEnabled { get; set; } = true;
    public bool   IsComment { get; set; } = false;
}

public class HostsFileEditorViewModel : ViewModelBase
{
    private static readonly string HostsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                     @"drivers\etc\hosts");
    private static readonly string BackupPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowsUtilityPack", "hosts.backup");

    private static readonly string[] BlocklistEntries =
    [
        "0.0.0.0 ads.google.com",
        "0.0.0.0 doubleclick.net",
        "0.0.0.0 googleadservices.com",
        "0.0.0.0 googlesyndication.com",
        "0.0.0.0 adservice.google.com",
        "0.0.0.0 ads.youtube.com",
        "0.0.0.0 pagead2.googlesyndication.com",
        "0.0.0.0 stats.g.doubleclick.net",
        "0.0.0.0 www.google-analytics.com",
        "0.0.0.0 ssl.google-analytics.com"
    ];

    private ObservableCollection<HostsEntry> _entries = [];
    private HostsEntry? _selectedEntry;
    private string _newIp       = string.Empty;
    private string _newHostname = string.Empty;
    private string _newComment  = string.Empty;
    private bool   _isModified;
    private string _statusMessage = string.Empty;

    public ObservableCollection<HostsEntry> Entries
    {
        get => _entries;
        private set => SetProperty(ref _entries, value);
    }

    public HostsEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    public string NewIp
    {
        get => _newIp;
        set => SetProperty(ref _newIp, value);
    }

    public string NewHostname
    {
        get => _newHostname;
        set => SetProperty(ref _newHostname, value);
    }

    public string NewComment
    {
        get => _newComment;
        set => SetProperty(ref _newComment, value);
    }

    public bool IsModified
    {
        get => _isModified;
        private set => SetProperty(ref _isModified, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand LoadCommand         { get; }
    public AsyncRelayCommand SaveCommand         { get; }
    public RelayCommand      AddEntryCommand     { get; }
    public RelayCommand      DeleteEntryCommand  { get; }
    public RelayCommand      ToggleEntryCommand  { get; }
    public RelayCommand      AddBlocklistCommand { get; }
    public AsyncRelayCommand RestoreBackupCommand { get; }

    public HostsFileEditorViewModel()
    {
        LoadCommand         = new AsyncRelayCommand(LoadAsync);
        SaveCommand         = new AsyncRelayCommand(SaveAsync);
        AddEntryCommand     = new RelayCommand(AddEntry);
        DeleteEntryCommand  = new RelayCommand(DeleteEntry, () => SelectedEntry != null);
        ToggleEntryCommand  = new RelayCommand(ToggleEntry, () => SelectedEntry != null);
        AddBlocklistCommand = new RelayCommand(AddBlocklist);
        RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);

        LoadCommand.Execute(null);
    }

    private async Task LoadAsync()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(HostsPath);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Entries.Clear();
                foreach (var line in lines)
                    ParseAndAdd(line);
                IsModified    = false;
                StatusMessage = $"Loaded {Entries.Count} entries from {HostsPath}.";
            });
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Access denied. Run the application as Administrator to edit the hosts file.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }

    private void ParseAndAdd(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        var trimmed = line.Trim();

        // Pure comment line (not a disabled entry)
        if (trimmed.StartsWith('#'))
        {
            // Try to detect disabled entries like "# 0.0.0.0 hostname"
            var inner = trimmed[1..].Trim();
            var parts = Regex.Split(inner, @"\s+");
            if (parts.Length >= 2 && IsIpAddress(parts[0]))
            {
                var commentPart = parts.Length >= 3 && parts[2].StartsWith('#')
                    ? string.Join(" ", parts.Skip(3))
                    : string.Empty;

                Entries.Add(new HostsEntry
                {
                    IpAddress = parts[0],
                    Hostname  = parts[1],
                    Comment   = commentPart,
                    IsEnabled = false,
                    IsComment = false
                });
                return;
            }

            Entries.Add(new HostsEntry
            {
                IpAddress = string.Empty,
                Hostname  = trimmed,
                Comment   = string.Empty,
                IsEnabled = true,
                IsComment = true
            });
            return;
        }

        // Normal entry
        var commentIdx = trimmed.IndexOf('#');
        var entryPart  = commentIdx >= 0 ? trimmed[..commentIdx].Trim() : trimmed;
        var comment    = commentIdx >= 0 ? trimmed[(commentIdx + 1)..].Trim() : string.Empty;

        var segments = Regex.Split(entryPart, @"\s+");
        if (segments.Length >= 2)
        {
            Entries.Add(new HostsEntry
            {
                IpAddress = segments[0],
                Hostname  = segments[1],
                Comment   = comment,
                IsEnabled = true,
                IsComment = false
            });
        }
    }

    private static bool IsIpAddress(string s) =>
        System.Net.IPAddress.TryParse(s, out _);

    private async Task SaveAsync()
    {
        try
        {
            await CreateBackupAsync();
            var content = BuildFileContent();
            await File.WriteAllTextAsync(HostsPath, content, Encoding.ASCII);
            IsModified    = false;
            StatusMessage = "Hosts file saved successfully.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Access denied. Run the application as Administrator to save the hosts file.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private static async Task CreateBackupAsync()
    {
        var backupDir = Path.GetDirectoryName(BackupPath);
        if (!string.IsNullOrWhiteSpace(backupDir))
            Directory.CreateDirectory(backupDir);

        if (File.Exists(HostsPath))
        {
            var source = await File.ReadAllTextAsync(HostsPath, Encoding.ASCII);
            await File.WriteAllTextAsync(BackupPath, source, Encoding.ASCII);
        }
    }

    private async Task RestoreBackupAsync()
    {
        try
        {
            if (!File.Exists(BackupPath))
            {
                StatusMessage = "No backup found.";
                return;
            }

            var backupContent = await File.ReadAllTextAsync(BackupPath, Encoding.ASCII);
            await File.WriteAllTextAsync(HostsPath, backupContent, Encoding.ASCII);
            await LoadAsync();
            StatusMessage = "Hosts file restored from backup.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Access denied. Run the application as Administrator to restore backup.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore failed: {ex.Message}";
        }
    }

    private string BuildFileContent()
    {
        var sb = new StringBuilder();
        foreach (var entry in Entries)
        {
            if (entry.IsComment)
            {
                sb.AppendLine(entry.Hostname);
                continue;
            }

            var line = $"{entry.IpAddress}\t{entry.Hostname}";
            if (!string.IsNullOrEmpty(entry.Comment))
                line += $"\t# {entry.Comment}";

            if (!entry.IsEnabled)
                line = "# " + line;

            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    private void AddEntry()
    {
        if (string.IsNullOrWhiteSpace(NewIp) || string.IsNullOrWhiteSpace(NewHostname))
        {
            StatusMessage = "IP Address and Hostname are required.";
            return;
        }

        Entries.Add(new HostsEntry
        {
            IpAddress = NewIp.Trim(),
            Hostname  = NewHostname.Trim(),
            Comment   = NewComment.Trim(),
            IsEnabled = true,
            IsComment = false
        });

        NewIp       = string.Empty;
        NewHostname = string.Empty;
        NewComment  = string.Empty;
        IsModified  = true;
        StatusMessage = "Entry added. Remember to Save.";
    }

    private void DeleteEntry()
    {
        if (SelectedEntry == null) return;
        Entries.Remove(SelectedEntry);
        SelectedEntry = null;
        IsModified    = true;
        StatusMessage = "Entry deleted. Remember to Save.";
    }

    private void ToggleEntry()
    {
        if (SelectedEntry == null) return;
        SelectedEntry.IsEnabled = !SelectedEntry.IsEnabled;

        // Force list refresh
        var idx = Entries.IndexOf(SelectedEntry);
        if (idx >= 0)
        {
            var e = SelectedEntry;
            Entries.RemoveAt(idx);
            Entries.Insert(idx, e);
            SelectedEntry = e;
        }

        IsModified    = true;
        StatusMessage = $"Entry '{SelectedEntry.Hostname}' toggled. Remember to Save.";
    }

    private void AddBlocklist()
    {
        foreach (var entry in BlocklistEntries)
        {
            var parts = entry.Split(' ', 2);
            if (parts.Length == 2 && !Entries.Any(e => e.Hostname == parts[1] && e.IpAddress == parts[0]))
            {
                Entries.Add(new HostsEntry
                {
                    IpAddress = parts[0],
                    Hostname  = parts[1],
                    Comment   = "Blocklist",
                    IsEnabled = true
                });
            }
        }
        IsModified    = true;
        StatusMessage = "Blocklist presets added. Remember to Save.";
    }
}
