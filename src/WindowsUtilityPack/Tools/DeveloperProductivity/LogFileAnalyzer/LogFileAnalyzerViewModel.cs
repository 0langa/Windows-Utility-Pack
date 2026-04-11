using System.Collections.ObjectModel;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.LogFileAnalyzer;

/// <summary>
/// ViewModel for line-based log file analysis.
/// </summary>
public sealed class LogFileAnalyzerViewModel : ViewModelBase
{
    private readonly ILogFileAnalyzerService _service;
    private readonly IClipboardService _clipboard;
    private readonly IUserDialogService _dialogs;

    private string _filePath = string.Empty;
    private string _searchText = string.Empty;
    private LogSeverity _minimumSeverity = LogSeverity.Unknown;
    private string _statusMessage = "Select a log file and click Analyze.";
    private bool _isBusy;
    private int _totalLines;
    private int _matchedLines;
    private int _errorCount;
    private int _warnCount;

    public ObservableCollection<LogEntryRow> Entries { get; } = [];

    public IReadOnlyList<LogSeverity> SeverityOptions { get; } = Enum.GetValues<LogSeverity>();

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public LogSeverity MinimumSeverity
    {
        get => _minimumSeverity;
        set => SetProperty(ref _minimumSeverity, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public int TotalLines
    {
        get => _totalLines;
        set => SetProperty(ref _totalLines, value);
    }

    public int MatchedLines
    {
        get => _matchedLines;
        set => SetProperty(ref _matchedLines, value);
    }

    public int ErrorCount
    {
        get => _errorCount;
        set => SetProperty(ref _errorCount, value);
    }

    public int WarnCount
    {
        get => _warnCount;
        set => SetProperty(ref _warnCount, value);
    }

    public RelayCommand BrowseCommand { get; }
    public AsyncRelayCommand AnalyzeCommand { get; }
    public RelayCommand CopySummaryCommand { get; }

    public LogFileAnalyzerViewModel(ILogFileAnalyzerService service, IClipboardService clipboard, IUserDialogService dialogs)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        BrowseCommand = new RelayCommand(_ => Browse());
        AnalyzeCommand = new AsyncRelayCommand(_ => AnalyzeAsync());
        CopySummaryCommand = new RelayCommand(_ => CopySummary());
    }

    internal async Task AnalyzeAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            _dialogs.ShowError("Log File Analyzer", "Select a file path first.");
            return;
        }

        IsBusy = true;
        try
        {
            LogSeverity? min = MinimumSeverity == LogSeverity.Unknown ? null : MinimumSeverity;
            var result = await _service.AnalyzeAsync(FilePath, SearchText, min).ConfigureAwait(true);

            TotalLines = result.TotalLines;
            MatchedLines = result.MatchedLines;
            ErrorCount = result.ErrorCount;
            WarnCount = result.WarnCount;

            Entries.Clear();
            foreach (var entry in result.Entries.Take(5_000))
            {
                Entries.Add(entry);
            }

            StatusMessage = $"Analyzed {TotalLines:N0} lines. Showing {Entries.Count:N0} matching entries.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Analysis failed.";
            _dialogs.ShowError("Log File Analyzer", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select log file",
            Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
            StatusMessage = "File selected. Click Analyze.";
        }
    }

    private void CopySummary()
    {
        var summary = $"Lines: {TotalLines}, Matched: {MatchedLines}, Errors: {ErrorCount}, Warnings: {WarnCount}";
        _clipboard.SetText(summary);
        StatusMessage = "Summary copied to clipboard.";
    }
}