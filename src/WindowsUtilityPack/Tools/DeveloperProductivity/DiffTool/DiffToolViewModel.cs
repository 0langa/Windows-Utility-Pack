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

public sealed class DiffToolViewModel : ViewModelBase, IDisposable, IViewLifetime
{
    private readonly IClipboardService _clipboardService;
    private CancellationTokenSource? _debounceCts;
    private const int DebounceMs = 500;
    private bool _disposed;

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

    public AsyncRelayCommand ComputeDiffCommand { get; }
    public RelayCommand SwapCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand CopyDiffCommand { get; }
    public AsyncRelayCommand LoadLeftFileCommand { get; }
    public AsyncRelayCommand LoadRightFileCommand { get; }

    public DiffToolViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;

        ComputeDiffCommand   = new AsyncRelayCommand(_ => RunDiffAsync());
        SwapCommand          = new RelayCommand(_ => Swap());
        ClearCommand         = new RelayCommand(_ => Clear());
        CopyDiffCommand      = new RelayCommand(_ => CopyDiff());
        LoadLeftFileCommand  = new AsyncRelayCommand(_ => LoadFileAsync(isLeft: true));
        LoadRightFileCommand = new AsyncRelayCommand(_ => LoadFileAsync(isLeft: false));
    }

    private async void ScheduleComputeDiff()
    {
        if (_disposed) return;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        try
        {
            await Task.Delay(DebounceMs, cts.Token);
            await RunDiffAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed) StatusMessage = $"Comparison failed: {ex.Message}"; }
    }

    internal async Task RunDiffAsync()
    {
        if (_disposed) return;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        _debounceCts = cts;
        var leftText = LeftText; var rightText = RightText;
        var ignoreWhitespace = IgnoreWhitespace; var ignoreCase = IgnoreCase;
        DiffLines.Clear();
        AddedCount = RemovedCount = UnchangedCount = 0;

        if (string.IsNullOrEmpty(LeftText) && string.IsNullOrEmpty(RightText))
        {
            StatusMessage = "Enter text in both panes to compare.";
            _debounceCts = null;
            return;
        }

        try
        {
        if (leftText.Length > BoundedTextFile.MaximumEditorCharacters || rightText.Length > BoundedTextFile.MaximumEditorCharacters)
            throw new InvalidDataException("Each comparison input is limited to 1,000,000 characters.");
        var lines = await Task.Run(() => ComputeDiff(leftText.Replace("\r\n", "\n").Split('\n'), rightText.Replace("\r\n", "\n").Split('\n'), ignoreWhitespace, ignoreCase, token), token);
        if (cts.IsCancellationRequested || _disposed) return;

        foreach (var line in lines)
            DiffLines.Add(line);

        AddedCount     = lines.Count(l => l.Type == DiffType.Added);
        RemovedCount   = lines.Count(l => l.Type == DiffType.Removed);
        UnchangedCount = lines.Count(l => l.Type == DiffType.Unchanged);

        StatusMessage = $"{AddedCount} added, {RemovedCount} removed, {UnchangedCount} unchanged";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!cts.IsCancellationRequested && !_disposed) StatusMessage = $"Comparison failed: {ex.Message}"; }
        finally { if (ReferenceEquals(_debounceCts, cts)) _debounceCts = null; }
    }

    internal static List<DiffLine> ComputeDiff(string[] left, string[] right, bool ignoreWs, bool ignoreCase, CancellationToken cancellationToken = default)
    {
        // Build LCS matrix
        int m = left.Length, n = right.Length;
        if ((long)m + n > 20_000) throw new InvalidDataException("Comparisons are limited to 20,000 total lines.");
        var leftNormalized = left.Select(s => Normalize(s, ignoreWs, ignoreCase)).ToArray();
        var rightNormalized = right.Select(s => Normalize(s, ignoreWs, ignoreCase)).ToArray();
        if (leftNormalized.SequenceEqual(rightNormalized))
            return right.Select((line, index) => new DiffLine { Content = line, Type = DiffType.Unchanged, LeftLineNumber = index + 1, RightLineNumber = index + 1 }).ToList();
        if ((long)(m + 1) * (n + 1) > 4_000_000)
            throw new InvalidDataException("This comparison is too large for the detailed diff. Compare smaller sections (up to 4,000,000 line pairs).");
        var dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int j = 1; j <= n; j++)
            {
                var lNorm = leftNormalized[i - 1];
                var rNorm = rightNormalized[j - 1];
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
                var lNorm = leftNormalized[li - 1];
                var rNorm = rightNormalized[ri - 1];

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

    private async Task LoadFileAsync(bool isLeft)
    {
        var dlg = new OpenFileDialog
        {
            Title  = isLeft ? "Open Left (Original) File" : "Open Right (Modified) File",
            Filter = "Text files (*.txt;*.cs;*.json;*.xml;*.yaml;*.md)|*.txt;*.cs;*.json;*.xml;*.yaml;*.md|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var content = await BoundedTextFile.ReadAsync(dlg.FileName);
            if (_disposed) return;
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
    public void Activate() { }
    public void Deactivate() { _debounceCts?.Cancel(); }
    public void Dispose() { _disposed = true; _debounceCts?.Cancel(); _debounceCts?.Dispose(); _debounceCts = null; }
}
