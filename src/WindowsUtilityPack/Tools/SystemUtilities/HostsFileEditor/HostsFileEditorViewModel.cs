using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.HostsFileEditor;

public class HostsEntry : ViewModelBase
{
    private string _ipAddress = string.Empty;
    private string _hostname  = string.Empty;
    private string _comment   = string.Empty;
    private bool   _isEnabled = true;
    private bool   _isComment;

    public string IpAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }
    public string Hostname  { get => _hostname;  set => SetProperty(ref _hostname,  value); }
    public string Comment   { get => _comment;   set => SetProperty(ref _comment,   value); }
    public bool   IsEnabled { get => _isEnabled;  set => SetProperty(ref _isEnabled,  value); }
    public bool   IsComment { get => _isComment;  set => SetProperty(ref _isComment,  value); }
}

public class HostsFileEditorViewModel : ViewModelBase, INavigationGuard
{
    private static readonly string DefaultHostsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                     @"drivers\etc\hosts");
    private static readonly string DefaultBackupPath =
        Path.Combine(AppDataPaths.RoamingDirectory, "hosts.backup");

    private readonly string _hostsPath;
    private readonly string _backupPath;
    private readonly IUserDialogService _dialogs;
    private readonly Dictionary<HostsEntry, (string Text, string Signature)> _originalLines = [];
    private byte[]? _loadedBytes;
    private Encoding _encoding = new UTF8Encoding(false, true);
    private string _newline = Environment.NewLine;
    private bool _loading, _busy;
    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); RelayCommand.RaiseCanExecuteChanged(); } }

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
        : this(null, null, autoLoad: true)
    {
    }

    internal HostsFileEditorViewModel(string? hostsPath, string? backupPath, bool autoLoad, IUserDialogService? dialogs = null)
    {
        _hostsPath = string.IsNullOrWhiteSpace(hostsPath) ? DefaultHostsPath : hostsPath;
        _backupPath = string.IsNullOrWhiteSpace(backupPath) ? DefaultBackupPath : backupPath;
        _dialogs = dialogs ?? new UserDialogService();
        Entries.CollectionChanged += (_, args) =>
        {
            if (args.OldItems is not null) foreach (HostsEntry entry in args.OldItems) entry.PropertyChanged -= EntryChanged;
            if (args.NewItems is not null) foreach (HostsEntry entry in args.NewItems) entry.PropertyChanged += EntryChanged;
            if (!_loading) IsModified = true;
        };

        LoadCommand         = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        SaveCommand         = new AsyncRelayCommand(SaveAsync, () => !IsBusy && _loadedBytes is not null);
        AddEntryCommand     = new RelayCommand(AddEntry, () => !IsBusy);
        DeleteEntryCommand  = new RelayCommand(DeleteEntry, () => !IsBusy && SelectedEntry != null);
        ToggleEntryCommand  = new RelayCommand(ToggleEntry, () => !IsBusy && SelectedEntry != null);
        AddBlocklistCommand = new RelayCommand(AddBlocklist, () => !IsBusy);
        RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync, () => !IsBusy);

        if (autoLoad)
        {
            LoadCommand.Execute(null);
        }
    }

    private async Task LoadAsync()
    {
        if (IsBusy || !ConfirmDiscard()) return;
        IsBusy = true;
        try
        {
            await LoadCoreAsync();
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Access denied. Run the application as Administrator to edit the hosts file.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private void EntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { if (!_loading) IsModified = true; }

    private async Task LoadCoreAsync()
    {
        var bytes = await Task.Run(() => ReadBounded(_hostsPath));
        using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        _encoding = reader.CurrentEncoding;
        // Preserve whether UTF-8 had a BOM; StreamReader's detected UTF-8 may otherwise add one.
        if (_encoding.CodePage == Encoding.UTF8.CodePage) _encoding = new UTF8Encoding(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }), true);
        _newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        _loading = true;
        try
        {
            foreach (var entry in Entries) entry.PropertyChanged -= EntryChanged;
            Entries.Clear(); _originalLines.Clear();
            using var lines = new StringReader(text);
            while (lines.ReadLine() is { } line)
            {
                var before = Entries.Count;
                ParseAndAdd(line);
                if (Entries.Count == before) Entries.Add(new HostsEntry { IsComment = true, Hostname = line });
                var entry = Entries[^1];
                _originalLines[entry] = (line, Signature(entry));
            }
            _loadedBytes = bytes;
            IsModified = false;
            StatusMessage = $"Loaded {Entries.Count} entries from {_hostsPath}.";
        }
        finally { _loading = false; }
    }

    private static byte[] ReadBounded(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 4 * 1024 * 1024) throw new InvalidDataException("Hosts files are limited to 4 MiB.");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes); return bytes;
    }
    private static string Signature(HostsEntry entry) => System.Text.Json.JsonSerializer.Serialize(new { entry.IpAddress, entry.Hostname, entry.Comment, entry.IsEnabled, entry.IsComment });

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
            var commentIndex = inner.IndexOf('#');
            var parts = Regex.Split(commentIndex < 0 ? inner : inner[..commentIndex], @"\s+").Where(p => p.Length > 0).ToArray();
            if (parts.Length >= 2 && IsIpAddress(parts[0]))
            {
                var commentPart = commentIndex < 0 ? string.Empty : inner[(commentIndex + 1)..].Trim();

                Entries.Add(new HostsEntry
                {
                    IpAddress = parts[0],
                    Hostname  = string.Join(" ", parts.Skip(1)),
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
                Hostname  = string.Join(" ", segments.Skip(1)),
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
        if (IsBusy) return;
        if (_loadedBytes is null) { StatusMessage = "Load the hosts file before saving changes."; return; }
        IsBusy = true;
        try
        {
            var content = BuildFileContent();
            var submitted = Entries.Select(Signature).ToArray();
            var original = _loadedBytes;
            var encoding = _encoding;
            var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(content)).ToArray();
            await Task.Run(() =>
            {
                if (!ReadBounded(_hostsPath).AsSpan().SequenceEqual(original)) throw new IOException("The hosts file changed outside this editor. Reload it before saving.");
                AtomicFile.WriteAllBytes(_backupPath, original);
                AtomicFile.WriteAllBytes(_hostsPath, bytes);
            });
            _loadedBytes = bytes;
            IsModified = !submitted.SequenceEqual(Entries.Select(Signature));
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
        finally { IsBusy = false; }
    }

    private async Task RestoreBackupAsync()
    {
        if (IsBusy || !_dialogs.Confirm("Restore hosts backup", "Replace the hosts file with the saved backup? The current file will be kept in a .before-restore copy.")) return;
        IsBusy = true;
        try
        {
            if (!File.Exists(_backupPath))
            {
                StatusMessage = "No backup found.";
                return;
            }

            await Task.Run(() =>
            {
                var bytes = ReadBounded(_backupPath);
                AtomicFile.WriteAllBytes(_backupPath + ".before-restore", ReadBounded(_hostsPath));
                AtomicFile.WriteAllBytes(_hostsPath, bytes);
            });
            await LoadCoreAsync();
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
        finally { IsBusy = false; }
    }

    private string BuildFileContent()
    {
        var sb = new StringBuilder();
        foreach (var entry in Entries)
        {
            if (_originalLines.TryGetValue(entry, out var original) && original.Signature == Signature(entry))
            { sb.Append(original.Text).Append(_newline); continue; }
            if (new[] { entry.IpAddress, entry.Hostname, entry.Comment }.Any(s => s.IndexOfAny(['\r', '\n', '\0']) >= 0))
                throw new InvalidDataException("Hosts entries must not contain line breaks or null characters.");
            if (entry.IsComment)
            {
                if (!string.IsNullOrWhiteSpace(entry.Hostname) && !entry.Hostname.TrimStart().StartsWith('#'))
                    throw new InvalidDataException("Comment lines must start with #.");
                sb.Append(entry.Hostname).Append(_newline);
                continue;
            }
            var names = entry.Hostname.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (!IsIpAddress(entry.IpAddress) || names.Length == 0 || names.Any(name => !IsValidHostname(name)))
                throw new InvalidDataException("Every hosts entry needs a valid IP address and hostname(s).");

            var line = $"{entry.IpAddress}\t{entry.Hostname}";
            if (!string.IsNullOrEmpty(entry.Comment))
                line += $"\t# {entry.Comment}";

            if (!entry.IsEnabled)
                line = "# " + line;

            sb.Append(line).Append(_newline);
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

        var ip = NewIp.Trim();
        var hostname = NewHostname.Trim();
        if (!IsIpAddress(ip))
        {
            StatusMessage = "Please enter a valid IP address.";
            return;
        }

        if (!IsValidHostname(hostname))
        {
            StatusMessage = "Please enter a valid hostname.";
            return;
        }

        if (Entries.Any(entry => !entry.IsComment
            && string.Equals(entry.IpAddress, ip, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Hostname, hostname, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "That hosts entry already exists.";
            return;
        }

        Entries.Add(new HostsEntry
        {
            IpAddress = ip,
            Hostname  = hostname,
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

    public bool CanNavigateAway()
    {
        return !IsBusy && ConfirmDiscard();
    }
    private bool ConfirmDiscard() => !IsModified || _dialogs.Confirm("Unsaved hosts changes", "Discard the unsaved hosts file changes?");

    private static bool IsValidHostname(string host)
    {
        if (host.Length is < 1 or > 253)
        {
            return false;
        }

        foreach (var label in host.Split('.'))
        {
            if (label.Length is < 1 or > 63)
            {
                return false;
            }

            if (!char.IsLetterOrDigit(label[0]) || !char.IsLetterOrDigit(label[^1]))
            {
                return false;
            }

            for (var i = 1; i < label.Length - 1; i++)
            {
                var ch = label[i];
                if (!char.IsLetterOrDigit(ch) && ch != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
