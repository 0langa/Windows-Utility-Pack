using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
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
/// Runs the regex pattern against the input text on every property change
/// (pattern, input, or option flags) for a live/interactive experience.
///
/// <see cref="RunRegex"/> is called synchronously because regex evaluation is
/// fast for typical inputs.  A debounce or background task can be added later
/// if very large inputs become a concern.
/// </summary>
public class RegexTesterViewModel : ViewModelBase
{
    private string _inputText     = "Hello World! This is a test string with numbers like 123 and 456.";
    private string _pattern       = @"\d+";
    private string _statusMessage = string.Empty;
    private bool   _ignoreCase;
    private bool   _multiline;
    private bool   _singleLine;
    private int    _matchCount;

    /// <summary>The text to search within.  Changing this re-runs the regex.</summary>
    public string InputText
    {
        get => _inputText;
        set { if (SetProperty(ref _inputText, value)) RunRegex(); }
    }

    /// <summary>The regex pattern string.  Changing this re-runs the regex.</summary>
    public string Pattern
    {
        get => _pattern;
        set { if (SetProperty(ref _pattern, value)) RunRegex(); }
    }

    /// <summary>Enables <see cref="RegexOptions.IgnoreCase"/>.  Changing this re-runs the regex.</summary>
    public bool IgnoreCase
    {
        get => _ignoreCase;
        set { if (SetProperty(ref _ignoreCase, value)) RunRegex(); }
    }

    /// <summary>Enables <see cref="RegexOptions.Multiline"/>.  Changing this re-runs the regex.</summary>
    public bool Multiline
    {
        get => _multiline;
        set { if (SetProperty(ref _multiline, value)) RunRegex(); }
    }

    /// <summary>Enables <see cref="RegexOptions.Singleline"/> (dot matches newline).  Changing this re-runs the regex.</summary>
    public bool SingleLine
    {
        get => _singleLine;
        set { if (SetProperty(ref _singleLine, value)) RunRegex(); }
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
    /// Evaluates <see cref="Pattern"/> against <see cref="InputText"/> using the current
    /// option flags and populates <see cref="Matches"/> and <see cref="StatusMessage"/>.
    /// Invalid patterns set a descriptive error in <see cref="StatusMessage"/> instead of throwing.
    /// </summary>
    private void RunRegex()
    {
        Matches.Clear();
        StatusMessage = string.Empty;
        MatchCount    = 0;

        if (string.IsNullOrEmpty(Pattern) || string.IsNullOrEmpty(InputText))
            return;

        try
        {
            var opts = RegexOptions.None;
            if (IgnoreCase)  opts |= RegexOptions.IgnoreCase;
            if (Multiline)   opts |= RegexOptions.Multiline;
            if (SingleLine)  opts |= RegexOptions.Singleline;

            var regex   = new Regex(Pattern, opts);
            var matches = regex.Matches(InputText);
            MatchCount  = matches.Count;

            foreach (Match m in matches)
            {
                // Build a readable summary of named/numbered capture groups (skip group[0] = full match).
                var groups = string.Join(", ", m.Groups.Cast<Group>()
                    .Skip(1).Select((g, i) => $"[{i + 1}]: \"{g.Value}\""));

                Matches.Add(new MatchItem
                {
                    Index  = m.Index,
                    Length = m.Length,
                    Value  = m.Value,
                    Groups = groups,
                });
            }

            StatusMessage = MatchCount == 0 ? "No matches found" : $"{MatchCount} match(es) found";
        }
        catch (RegexParseException ex)
        {
            StatusMessage = $"Pattern error: {ex.Message}";
        }
    }
}
