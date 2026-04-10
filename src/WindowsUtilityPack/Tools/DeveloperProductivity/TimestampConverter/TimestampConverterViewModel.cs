using System.Threading;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.TimestampConverter;

public sealed class TimestampConverterViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboardService;
    private CancellationTokenSource? _debounceCts;
    private const int DebounceMs = 400;

    private string _unixSeconds = string.Empty;
    private string _unixMilliseconds = string.Empty;
    private string _humanReadableUtc = string.Empty;
    private string _humanReadableLocal = string.Empty;
    private string _relativeTime = string.Empty;
    private string _customFormat = "yyyy-MM-dd HH:mm:ss";
    private string _customFormatResult = string.Empty;
    private DateTime? _parsedDt;
    private string _dateInput = string.Empty;
    private string _parsedUnixSeconds = string.Empty;
    private string _parsedUnixMs = string.Empty;
    private bool _isCurrentTime;

    public string UnixSeconds
    {
        get => _unixSeconds;
        set
        {
            if (SetProperty(ref _unixSeconds, value))
                ScheduleConvert();
        }
    }

    public string UnixMilliseconds
    {
        get => _unixMilliseconds;
        private set => SetProperty(ref _unixMilliseconds, value);
    }

    public string HumanReadableUtc
    {
        get => _humanReadableUtc;
        private set => SetProperty(ref _humanReadableUtc, value);
    }

    public string HumanReadableLocal
    {
        get => _humanReadableLocal;
        private set => SetProperty(ref _humanReadableLocal, value);
    }

    public string RelativeTime
    {
        get => _relativeTime;
        private set => SetProperty(ref _relativeTime, value);
    }

    public string CustomFormat
    {
        get => _customFormat;
        set
        {
            if (SetProperty(ref _customFormat, value))
                ApplyCustomFormat();
        }
    }

    public string CustomFormatResult
    {
        get => _customFormatResult;
        private set => SetProperty(ref _customFormatResult, value);
    }

    public string DateInput
    {
        get => _dateInput;
        set => SetProperty(ref _dateInput, value);
    }

    public string ParsedUnixSeconds
    {
        get => _parsedUnixSeconds;
        private set => SetProperty(ref _parsedUnixSeconds, value);
    }

    public string ParsedUnixMs
    {
        get => _parsedUnixMs;
        private set => SetProperty(ref _parsedUnixMs, value);
    }

    public bool IsCurrentTime
    {
        get => _isCurrentTime;
        set => SetProperty(ref _isCurrentTime, value);
    }

    public RelayCommand UseNowCommand { get; }
    public RelayCommand CopyUnixCommand { get; }
    public RelayCommand CopyHumanCommand { get; }
    public RelayCommand ParseDateCommand { get; }
    public RelayCommand CopyParsedUnixCommand { get; }

    public TimestampConverterViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;

        UseNowCommand = new RelayCommand(_ => UseNow());
        CopyUnixCommand = new RelayCommand(_ => _clipboardService.SetText(UnixSeconds));
        CopyHumanCommand = new RelayCommand(_ => _clipboardService.SetText(HumanReadableUtc));
        ParseDateCommand = new RelayCommand(_ => ParseDate());
        CopyParsedUnixCommand = new RelayCommand(_ => _clipboardService.SetText(ParsedUnixSeconds));

        // Start with current time
        UseNow();
    }

    private void UseNow()
    {
        UnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        IsCurrentTime = true;
    }

    private async void ScheduleConvert()
    {
        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;

        try
        {
            await Task.Delay(DebounceMs, cts.Token);
            ConvertUnixToHuman();
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private void ConvertUnixToHuman()
    {
        if (!long.TryParse(UnixSeconds, out var ts))
        {
            HumanReadableUtc = "Invalid timestamp";
            HumanReadableLocal = string.Empty;
            UnixMilliseconds = string.Empty;
            RelativeTime = string.Empty;
            CustomFormatResult = string.Empty;
            _parsedDt = null;
            return;
        }

        try
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds(ts);
            _parsedDt = dto.UtcDateTime;

            UnixMilliseconds = (ts * 1000L).ToString();
            HumanReadableUtc = dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            HumanReadableLocal = dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss zzz");
            RelativeTime = GetRelativeTime(dto);
            ApplyCustomFormat();
        }
        catch
        {
            HumanReadableUtc = "Out of range";
            HumanReadableLocal = string.Empty;
            UnixMilliseconds = string.Empty;
            RelativeTime = string.Empty;
        }
    }

    private void ApplyCustomFormat()
    {
        if (_parsedDt is null) { CustomFormatResult = string.Empty; return; }
        try
        {
            CustomFormatResult = _parsedDt.Value.ToString(CustomFormat);
        }
        catch
        {
            CustomFormatResult = "Invalid format string";
        }
    }

    private static string GetRelativeTime(DateTimeOffset dt)
    {
        var diff = DateTimeOffset.UtcNow - dt;
        var absDiff = diff.Duration();

        if (absDiff.TotalSeconds < 60)
            return diff.TotalSeconds >= 0 ? $"{(int)absDiff.TotalSeconds} seconds ago" : $"in {(int)absDiff.TotalSeconds} seconds";
        if (absDiff.TotalMinutes < 60)
            return diff.TotalMinutes >= 0 ? $"{(int)absDiff.TotalMinutes} minutes ago" : $"in {(int)absDiff.TotalMinutes} minutes";
        if (absDiff.TotalHours < 24)
            return diff.TotalHours >= 0 ? $"{(int)absDiff.TotalHours} hours ago" : $"in {(int)absDiff.TotalHours} hours";
        if (absDiff.TotalDays < 30)
            return diff.TotalDays >= 0 ? $"{(int)absDiff.TotalDays} days ago" : $"in {(int)absDiff.TotalDays} days";
        if (absDiff.TotalDays < 365)
            return diff.TotalDays >= 0 ? $"{(int)(absDiff.TotalDays / 30)} months ago" : $"in {(int)(absDiff.TotalDays / 30)} months";

        return diff.TotalDays >= 0 ? $"{(int)(absDiff.TotalDays / 365)} years ago" : $"in {(int)(absDiff.TotalDays / 365)} years";
    }

    private void ParseDate()
    {
        if (string.IsNullOrWhiteSpace(DateInput))
        {
            ParsedUnixSeconds = string.Empty;
            ParsedUnixMs = string.Empty;
            return;
        }

        try
        {
            DateTimeOffset parsed;
            if (DateTimeOffset.TryParse(DateInput, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedOffset))
            {
                parsed = parsedOffset;
            }
            else if (DateTime.TryParse(DateInput, out var dt))
            {
                parsed = new DateTimeOffset(dt, TimeSpan.Zero);
            }
            else
            {
                ParsedUnixSeconds = "Could not parse date";
                ParsedUnixMs = string.Empty;
                return;
            }

            var seconds = parsed.ToUnixTimeSeconds();
            ParsedUnixSeconds = seconds.ToString();
            ParsedUnixMs = (seconds * 1000L).ToString();
        }
        catch
        {
            ParsedUnixSeconds = "Parse error";
            ParsedUnixMs = string.Empty;
        }
    }
}
