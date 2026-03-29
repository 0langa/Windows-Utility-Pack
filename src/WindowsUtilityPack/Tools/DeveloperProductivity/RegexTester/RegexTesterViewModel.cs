using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.RegexTester;

public class MatchItem
{
    public int Index { get; init; }
    public int Length { get; init; }
    public string Value { get; init; } = string.Empty;
    public string Groups { get; init; } = string.Empty;
}

public class RegexTesterViewModel : ViewModelBase
{
    private string _inputText = "Hello World! This is a test string with numbers like 123 and 456.";
    private string _pattern = @"\d+";
    private string _options = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _ignoreCase;
    private bool _multiline;
    private bool _singleLine;
    private int _matchCount;

    public string InputText
    {
        get => _inputText;
        set { if (SetProperty(ref _inputText, value)) RunRegex(); }
    }

    public string Pattern
    {
        get => _pattern;
        set { if (SetProperty(ref _pattern, value)) RunRegex(); }
    }

    public bool IgnoreCase
    {
        get => _ignoreCase;
        set { if (SetProperty(ref _ignoreCase, value)) RunRegex(); }
    }

    public bool Multiline
    {
        get => _multiline;
        set { if (SetProperty(ref _multiline, value)) RunRegex(); }
    }

    public bool SingleLine
    {
        get => _singleLine;
        set { if (SetProperty(ref _singleLine, value)) RunRegex(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int MatchCount
    {
        get => _matchCount;
        set => SetProperty(ref _matchCount, value);
    }

    public ObservableCollection<MatchItem> Matches { get; } = [];

    public RelayCommand ClearCommand { get; }

    public RegexTesterViewModel()
    {
        ClearCommand = new RelayCommand(_ => { InputText = string.Empty; Pattern = string.Empty; });
        RunRegex();
    }

    private void RunRegex()
    {
        Matches.Clear();
        StatusMessage = string.Empty;
        MatchCount = 0;

        if (string.IsNullOrEmpty(Pattern) || string.IsNullOrEmpty(InputText))
            return;

        try
        {
            var opts = RegexOptions.None;
            if (IgnoreCase) opts |= RegexOptions.IgnoreCase;
            if (Multiline) opts |= RegexOptions.Multiline;
            if (SingleLine) opts |= RegexOptions.Singleline;

            var regex = new Regex(Pattern, opts);
            var matches = regex.Matches(InputText);
            MatchCount = matches.Count;

            foreach (Match m in matches)
            {
                var groups = string.Join(", ", m.Groups.Cast<Group>()
                    .Skip(1).Select((g, i) => $"[{i + 1}]: \"{g.Value}\""));

                Matches.Add(new MatchItem
                {
                    Index = m.Index,
                    Length = m.Length,
                    Value = m.Value,
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
