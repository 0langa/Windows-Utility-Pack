using System.Windows.Input;
using System.Text.Json;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Supported shell-level hotkey actions.
/// </summary>
public enum ShellHotkeyAction
{
    OpenCommandPalette,
    QuickScreenshot,
    OpenScreenshotAnnotator,
    ToggleMainWindow,
    OpenSettings,
    NavigateHome,
    OpenActivityLog,
    OpenTaskMonitor,
}

/// <summary>
/// Provides shell hotkey configuration, validation, and lookup.
/// </summary>
public interface IHotkeyService
{
    event EventHandler? BindingsChanged;

    bool HotkeysEnabled { get; set; }

    IReadOnlyList<HotkeyBindingSetting> GetBindings();

    IReadOnlyList<HotkeyBindingSetting> GetDefaultBindings();

    (bool Success, string Error) SaveBindings(IReadOnlyList<HotkeyBindingSetting> bindings);

    /// <summary>
    /// Exports current hotkey configuration as JSON.
    /// </summary>
    string ExportProfileJson();

    /// <summary>
    /// Imports hotkey configuration from a JSON payload.
    /// </summary>
    (bool Success, string Error, int ImportedCount) ImportProfileJson(string json);

    bool TryMatch(Key key, ModifierKeys modifiers, out ShellHotkeyAction action);
}

/// <summary>
/// Default hotkey settings service backed by app settings.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISettingsService _settings;
    private readonly object _sync = new();
    private List<HotkeyBindingSetting>? _cachedBindings;
    public event EventHandler? BindingsChanged;

    public HotkeyService(ISettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool HotkeysEnabled
    {
        get => _settings.Load().HotkeysEnabled;
        set
        {
            var settings = _settings.Load();
            if (settings.HotkeysEnabled == value)
            {
                return;
            }

            settings.HotkeysEnabled = value;
            _settings.Save(settings);
            BindingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<HotkeyBindingSetting> GetBindings()
    {
        lock (_sync)
        {
            _cachedBindings ??= LoadOrDefaults();
            return _cachedBindings
                .Select(Clone)
                .ToList();
        }
    }

    public IReadOnlyList<HotkeyBindingSetting> GetDefaultBindings()
    {
        return
        [
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenCommandPalette.ToString(), Gesture = "Ctrl+K", Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.QuickScreenshot.ToString(), Gesture = "Ctrl+Shift+S", Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenScreenshotAnnotator.ToString(), Gesture = "Ctrl+Shift+A", Enabled = false },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.ToggleMainWindow.ToString(), Gesture = "Ctrl+Shift+Space", Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenSettings.ToString(), Gesture = "Ctrl+OemComma", Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.NavigateHome.ToString(), Gesture = "Ctrl+H", Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenActivityLog.ToString(), Gesture = "Ctrl+Shift+L", Enabled = true },
            new HotkeyBindingSetting { Action = ShellHotkeyAction.OpenTaskMonitor.ToString(), Gesture = "Ctrl+Shift+M", Enabled = true },
        ];
    }

    public (bool Success, string Error) SaveBindings(IReadOnlyList<HotkeyBindingSetting> bindings)
    {
        if (bindings is null)
        {
            return (false, "Bindings payload is required.");
        }

        var normalized = bindings
            .Select(Clone)
            .ToList();

        var validation = Validate(normalized);
        if (!validation.Success)
        {
            return validation;
        }

        lock (_sync)
        {
            var settings = _settings.Load();
            settings.HotkeyBindings = normalized;
            _settings.Save(settings);
            _cachedBindings = normalized;
        }

        BindingsChanged?.Invoke(this, EventArgs.Empty);
        return (true, string.Empty);
    }

    public string ExportProfileJson()
    {
        var profile = new HotkeyProfileDocument
        {
            Version = 1,
            ExportedUtc = DateTime.UtcNow,
            HotkeysEnabled = HotkeysEnabled,
            Bindings = GetBindings().Select(Clone).ToList(),
        };

        return JsonSerializer.Serialize(profile, JsonOptions);
    }

    public (bool Success, string Error, int ImportedCount) ImportProfileJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (false, "Profile JSON is required.", 0);
        }

        try
        {
            var profile = JsonSerializer.Deserialize<HotkeyProfileDocument>(json, JsonOptions);
            if (profile is null)
            {
                return (false, "Profile JSON is invalid.", 0);
            }

            var bindings = (profile.Bindings ?? [])
                .Select(Clone)
                .ToList();

            if (bindings.Count == 0)
            {
                return (false, "Profile does not contain any bindings.", 0);
            }

            var validation = Validate(bindings);
            if (!validation.Success)
            {
                return (false, validation.Error, 0);
            }

            lock (_sync)
            {
                var settings = _settings.Load();
                settings.HotkeysEnabled = profile.HotkeysEnabled;
                settings.HotkeyBindings = bindings;
                _settings.Save(settings);
                _cachedBindings = bindings;
            }

            BindingsChanged?.Invoke(this, EventArgs.Empty);
            return (true, string.Empty, bindings.Count);
        }
        catch (JsonException)
        {
            return (false, "Profile JSON could not be parsed.", 0);
        }
    }

    public bool TryMatch(Key key, ModifierKeys modifiers, out ShellHotkeyAction action)
    {
        action = default;
        if (!HotkeysEnabled)
        {
            return false;
        }

        foreach (var binding in GetBindings().Where(b => b.Enabled))
        {
            if (!Enum.TryParse<ShellHotkeyAction>(binding.Action, out var parsedAction))
            {
                continue;
            }

            if (!TryParseGesture(binding.Gesture, out var gesture))
            {
                continue;
            }

            if (gesture.Key == key && gesture.Modifiers == modifiers)
            {
                action = parsedAction;
                return true;
            }
        }

        return false;
    }

    private List<HotkeyBindingSetting> LoadOrDefaults()
    {
        var settings = _settings.Load();
        var configured = settings.HotkeyBindings ?? [];
        if (configured.Count == 0)
        {
            var defaults = GetDefaultBindings().Select(Clone).ToList();
            settings.HotkeyBindings = defaults;
            _settings.Save(settings);
            return defaults;
        }

        return configured.Select(Clone).ToList();
    }

    private static (bool Success, string Error) Validate(IReadOnlyList<HotkeyBindingSetting> bindings)
    {
        var usedGestures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var binding in bindings)
        {
            if (!Enum.TryParse<ShellHotkeyAction>(binding.Action, out _))
            {
                return (false, $"Unknown action: {binding.Action}");
            }

            if (string.IsNullOrWhiteSpace(binding.Gesture))
            {
                continue;
            }

            if (!TryParseGesture(binding.Gesture, out var parsedGesture))
            {
                return (false, $"Invalid gesture: {binding.Gesture}");
            }

            var canonical = $"{parsedGesture.Modifiers}+{parsedGesture.Key}";
            if (binding.Enabled && usedGestures.TryGetValue(canonical, out var existingAction))
            {
                return (false, $"Hotkey collision between '{existingAction}' and '{binding.Action}' ({binding.Gesture}).");
            }

            if (binding.Enabled)
            {
                usedGestures[canonical] = binding.Action;
            }
        }

        return (true, string.Empty);
    }

    private static bool TryParseGesture(string gestureText, out KeyGesture gesture)
    {
        gesture = default!;
        try
        {
            var converter = new KeyGestureConverter();
            var converted = converter.ConvertFromString(gestureText);
            if (converted is not KeyGesture parsed)
            {
                return false;
            }

            gesture = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static HotkeyBindingSetting Clone(HotkeyBindingSetting value)
        => new()
        {
            Action = value.Action,
            Gesture = value.Gesture,
            Enabled = value.Enabled,
        };

    private sealed class HotkeyProfileDocument
    {
        public int Version { get; init; } = 1;

        public DateTime ExportedUtc { get; init; }

        public bool HotkeysEnabled { get; init; } = true;

        public List<HotkeyBindingSetting> Bindings { get; init; } = [];
    }
}
