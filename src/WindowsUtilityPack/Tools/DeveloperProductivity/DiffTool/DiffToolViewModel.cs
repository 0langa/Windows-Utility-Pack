using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.DiffTool;

public enum DiffType { Unchanged, Added, Removed }

public class DiffLine
{
    public int? LeftLineNumber { get; set; }
    public int? RightLineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DiffType Type { get; set; }
}

public sealed class DiffToolViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboardService;
    private CancellationTokenSource? _debounceCts;
    private const int DebounceMs = 500;

    private string _leftText = string.Empty;
    private string _rightText = string.Empty;
    private string _leftLabel = "Original";
    private string _rightLabel = "Modified";
    private int _addedCount;
    private int _removedCount;
    private int _unchangedCount;
    private bool _ignoreWhitespace;
    private bool _ignoreCase;
    private string _statusMessage = "Enter text in both panes to compare.";

    public string LeftText
    {
        get => _leftText;
        set
        {
            if (SetProperty(ref _leftText, value))
                ScheduleComputeDiff();
        }
    }

    public string RightText
    {
        get => _rightText;
        set
        {
            if (SetProperty(ref _rightText, value))
                ScheduleComputeDiff();
        }
    }

    public string LeftLabel
    {
        get => _leftLabel;
        set => SetProperty(ref _leftLabel, value);
    }

    public string RightLabel
    {
        get => _rightLabel;
        set => SetProperty(ref _rightLabel, value);
    }

    public ObservableCollection<DiffLine> DiffLines { get; } = [];

    public int AddedCount
    {
        get => _addedCount;
        private set => SetProperty(ref _addedCount, value);
    }

    public int RemovedCount
    {
        get => _removedCount;
        private set => SetProperty(ref _removedCount, value);
    }

    public int UnchangedCount
    {
        get => _unchangedCount;
        private set => SetProperty(ref _unchangedCount, value);
    }

    public bool IgnoreWhitespace
    {
        get => _ignoreWhitespace;
        set
        {
            if (SetProperty(ref _ignoreWhitespace, value))
                ScheduleComputeDiff();
        }
    }

    public bool IgnoreCase
    {
        get => _ignoreCase;
        set
        {
            if (SetProperty(ref _ignoreCase, value))
                ScheduleComputeDiff();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand ComputeDiffCommand { get; }
    public RelayCommand SwapCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand CopyDiffCommand { get; }
    public RelayCommand LoadLeftFileCommand { get; }
    public RelayCommand LoadRightFileCommand { get; }

    public DiffToolViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;

        ComputeDiffCommand   = new RelayCommand(_ => RunDiff());
        SwapCommand          = new RelayCommand(_ => Swap());
        ClearCommand         = new RelayCommand(_ => Clear());
        CopyDiffCommand      = new RelayCommand(_ => CopyDiff());
        LoadLeftFileCommand  = new RelayCommand(_ => LoadFile(isLeft: true));
        LoadRightFileCommand = new RelayCommand(_ => LoadFile(isLeft: false));
    }

    private async void ScheduleComputeDiff()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        try
        {
            await Task.Delay(DebounceMs, cts.Token);
            RunDiff();
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private void RunDiff()
    {
        DiffLines.Clear();
        AddedCount = RemovedCount = UnchangedCount = 0;

        if (string.IsNullOrEmpty(LeftText) && string.IsNullOrEmpty(RightText))
        {
            StatusMessage = "Enter text in both panes to compare.";
            return;
        }

        var leftLines  = (LeftText  ?? string.Empty).Split('\n');
        var rightLines = (RightText ?? string.Empty).Split('\n');

        var lines = ComputeDiff(leftLines, rightLines, _ignoreWhitespace, _ignoreCase);

        foreach (var line in lines)
            DiffLines.Add(line);

        AddedCount     = lines.Count(l => l.Type == DiffType.Added);
        RemovedCount   = lines.Count(l => l.Type == DiffType.Removed);
        UnchangedCount = lines.Count(l => l.Type == DiffType.Unchanged);

        StatusMessage = $"{AddedCount} added, {RemovedCount} removed, {UnchangedCount} unchanged";
    }

    private static List<DiffLine> ComputeDiff(string[] left, string[] right, bool ignoreWs, bool ignoreCase)
    {
        // Build LCS matrix
        int m = left.Length, n = right.Length;
        var dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                var lNorm = Normalize(left[i - 1],  ignoreWs, ignoreCase);
                var rNorm = Normalize(right[j - 1], ignoreWs, ignoreCase);
                dp[i, j] = lNorm == rNorm ? dp[i - 1, j - 1] + 1 : Math.Max(dp[i - 1, j], dp[i, j - 1]);
            }
        }

        // Trace back
        var result = new List<DiffLine>();
        int li = m, ri = n;
        int leftNum = m, rightNum = n;

        while (li > 0 || ri > 0)
        {
            if (li > 0 && ri > 0)
            {
                var lNorm = Normalize(left[li - 1],  ignoreWs, ignoreCase);
                var rNorm = Normalize(right[ri - 1], ignoreWs, ignoreCase);

                if (lNorm == rNorm)
                {
                    result.Add(new DiffLine { LeftLineNumber = li, RightLineNumber = ri, Content = right[ri - 1], Type = DiffType.Unchanged });
                    li--; ri--;
                }
                else if (dp[li - 1, ri] >= dp[li, ri - 1])
                {
                    result.Add(new DiffLine { LeftLineNumber = li, Content = left[li - 1], Type = DiffType.Removed });
                    li--;
                }
                else
                {
                    result.Add(new DiffLine { RightLineNumber = ri, Content = right[ri - 1], Type = DiffType.Added });
                    ri--;
                }
            }
            else if (li > 0)
            {
                result.Add(new DiffLine { LeftLineNumber = li, Content = left[li - 1], Type = DiffType.Removed });
                li--;
            }
            else
            {
                result.Add(new DiffLine { RightLineNumber = ri, Content = right[ri - 1], Type = DiffType.Added });
                ri--;
            }
        }

        result.Reverse();
        return result;
    }

    private static string Normalize(string s, bool ignoreWs, bool ignoreCase)
    {
        if (ignoreWs) s = s.Trim();
        if (ignoreCase) s = s.ToLowerInvariant();
        return s;
    }

    private void Swap()
    {
        var tmp = LeftText;
        LeftText  = RightText;
        RightText = tmp;
        var lbl = LeftLabel;
        LeftLabel  = RightLabel;
        RightLabel = lbl;
    }

    private void Clear()
    {
        LeftText = RightText = string.Empty;
        DiffLines.Clear();
        AddedCount = RemovedCount = UnchangedCount = 0;
        StatusMessage = "Enter text in both panes to compare.";
    }

    private void CopyDiff()
    {
        var sb = new StringBuilder();
        foreach (var line in DiffLines)
        {
            var prefix = line.Type switch
            {
                DiffType.Added   => "+ ",
                DiffType.Removed => "- ",
                _                => "  "
            };
            sb.AppendLine(prefix + line.Content);
        }
        _clipboardService.SetText(sb.ToString());
    }

    private void LoadFile(bool isLeft)
    {
        var dlg = new OpenFileDialog
        {
            Title  = isLeft ? "Open Left (Original) File" : "Open Right (Modified) File",
            Filter = "Text files (*.txt;*.cs;*.json;*.xml;*.yaml;*.md)|*.txt;*.cs;*.json;*.xml;*.yaml;*.md|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var content = File.ReadAllText(dlg.FileName);
            if (isLeft)
            {
                LeftText  = content;
                LeftLabel = Path.GetFileName(dlg.FileName);
            }
            else
            {
                RightText  = content;
                RightLabel = Path.GetFileName(dlg.FileName);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading file: {ex.Message}";
        }
    }
}
