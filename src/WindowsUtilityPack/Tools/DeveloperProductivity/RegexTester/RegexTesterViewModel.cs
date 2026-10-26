using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.RegexTester;

/// <summary>Represents a single regex match result shown in the results list.</summary>
public class MatchItem
{
    public int    Index  { get; init; }
    public int    Length { get; init; }
    public string Value  { get; init; } = string.Empty;

    /// <summary>Comma-separated list of named/numbered capture group values, if any.</summary>
    public string Groups { get; init; } = string.Empty;
}

/// <summary>
/// ViewModel for the Regex Tester tool.
///
/// Property changes (pattern, input, or option flags) schedule a debounced regex
/// evaluation via <see cref="ScheduleRunRegex"/> so that rapid typing does not
/// trigger an evaluation on every keystroke.  The debounce window is
/// <see cref="DebounceMs"/> milliseconds.
///
/// A per-evaluation timeout of <see cref="RegexMatchTimeout"/> guards against
/// catastrophic backtracking (ReDoS) caused by pathological patterns.
/// </summary>
public class RegexTesterViewModel : ViewModelBase, IDisposable, IViewLifetime
{
    // Maximum time allowed for a single regex evaluation.
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);

    // How long to wait after the last property change before running the regex.
    private const int DebounceMs = 250;

    private CancellationTokenSource? _debounceCts;
    private readonly SemaphoreSlim _evaluationGate = new(1, 1);
    private bool _disposed;

    private string _inputText     = "Hello World! This is a test string with numbers like 123 and 456.";
    private string _pattern       = @"\d+";
    private string _statusMessage = string.Empty;
    private bool   _ignoreCase;
    private bool   _multiline;
    private bool   _singleLine;
    private int    _matchCount;

    /// <summary>The text to search within.  Changing this schedules a debounced regex run.</summary>
    public string InputText
    {
        get => _inputText;
        set { if (SetProperty(ref _inputText, value)) ScheduleRunRegex(); }
    }

    /// <summary>The regex pattern string.  Changing this schedules a debounced regex run.</summary>
    public string Pattern
    {
        get => _pattern;
        set { if (SetProperty(ref _pattern, value)) ScheduleRunRegex(); }
    }

    /// <summary>Enables <see cref="RegexOptions.IgnoreCase"/>.</summary>
    public bool IgnoreCase
    {
        get => _ignoreCase;
        set { if (SetProperty(ref _ignoreCase, value)) ScheduleRunRegex(); }
    }

    /// <summary>Enables <see cref="RegexOptions.Multiline"/>.</summary>
    public bool Multiline
    {
        get => _multiline;
        set { if (SetProperty(ref _multiline, value)) ScheduleRunRegex(); }
    }

    /// <summary>Enables <see cref="RegexOptions.Singleline"/> (dot matches newline).</summary>
    public bool SingleLine
    {
        get => _singleLine;
        set { if (SetProperty(ref _singleLine, value)) ScheduleRunRegex(); }
    }

    /// <summary>Status message shown below the results — either a match count or an error message.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Total number of matches found, or 0 on error/no match.</summary>
    public int MatchCount
    {
        get => _matchCount;
        set => SetProperty(ref _matchCount, value);
    }

    /// <summary>The individual match items displayed in the results list.</summary>
    public ObservableCollection<MatchItem> Matches { get; } = [];

    /// <summary>Clears both the input text and the pattern, resetting the tool to a blank state.</summary>
    public RelayCommand ClearCommand { get; }

    public RegexTesterViewModel()
    {
        ClearCommand = new RelayCommand(_ => { InputText = string.Empty; Pattern = string.Empty; });
        // Run once immediately so the default example pattern shows results.
        RunRegex();
    }

    /// <summary>
    /// Cancels any pending debounced evaluation and schedules a new one.
    /// The actual <see cref="RunRegex"/> fires after <see cref="DebounceMs"/> ms of inactivity.
    /// </summary>
    private async void ScheduleRunRegex()
    {
        if (_disposed) return;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        _debounceCts = cts;
        var pattern = Pattern; var input = InputText; var options = GetOptions();
        Matches.Clear(); MatchCount = 0; StatusMessage = "Evaluating…";
        try
        {
            await Task.Delay(DebounceMs, token);
            await _evaluationGate.WaitAsync(token);
            Evaluation result;
            try { result = await Task.Run(() => Evaluate(pattern, input, options, token), token); }
            finally { _evaluationGate.Release(); }
            if (!token.IsCancellationRequested && !_disposed) Apply(result);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!token.IsCancellationRequested && !_disposed) StatusMessage = $"Evaluation failed: {ex.Message}"; }
        finally { if (ReferenceEquals(_debounceCts, cts)) _debounceCts = null; cts.Dispose(); }
    }

    internal void RunRegex()
    {
        _debounceCts?.Cancel();
        Apply(Evaluate(Pattern, InputText, GetOptions(), CancellationToken.None));
    }
    private RegexOptions GetOptions() => (IgnoreCase ? RegexOptions.IgnoreCase : 0) |
        (Multiline ? RegexOptions.Multiline : 0) | (SingleLine ? RegexOptions.Singleline : 0);
    private sealed record Evaluation(List<MatchItem> Matches, string Status);
    private static Evaluation Evaluate(string pattern, string input, RegexOptions options, CancellationToken token)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(input)) return new([], "");
        if (input.Length > 1_000_000 || pattern.Length > 4096)
            return new([], "Input is limited to 1,000,000 characters and patterns to 4,096 characters.");
        try
        {
            var regex = new Regex(pattern, options, RegexMatchTimeout);
            var results = new List<MatchItem>();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (var match = regex.Match(input); match.Success; match = match.NextMatch())
            {
                token.ThrowIfCancellationRequested();
                if (results.Count == 1000 || timer.Elapsed > TimeSpan.FromSeconds(2))
                    return new(results, $"Showing the first {results.Count} matches; result or time limit reached.");
                static string Preview(string text, int max) => text.Length <= max ? text : text[..max] + "…";
                results.Add(new MatchItem
                {
                    Index = match.Index, Length = match.Length, Value = Preview(match.Value, 512),
                    Groups = string.Join(", ", match.Groups.Cast<Group>().Skip(1).Take(32).Select((g, i) => $"[{i + 1}]: \"{Preview(g.Value, 128)}\""))
                });
            }
            return new(results, results.Count == 0 ? "No matches found" : $"{results.Count} match(es) found");
        }
        catch (RegexParseException ex) { return new([], $"Pattern error: {ex.Message}"); }
        catch (RegexMatchTimeoutException) { return new([], "Match timed out — the pattern may cause catastrophic backtracking."); }
    }
    private void Apply(Evaluation result)
    {
        Matches.Clear();
        foreach (var match in result.Matches) Matches.Add(match);
        MatchCount = result.Matches.Count; StatusMessage = result.Status;
    }
    public void Activate() { }
    public void Deactivate() { _debounceCts?.Cancel(); }
    public void Dispose() { _disposed = true; _debounceCts?.Cancel(); _debounceCts?.Dispose(); _debounceCts = null; }
}