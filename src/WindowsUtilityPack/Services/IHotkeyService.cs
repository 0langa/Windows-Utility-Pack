using System.Windows.Input;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Supported shell-level hotkey actions.
/// </summary>
public enum ShellHotkeyAction
{
    OpenCommandPalette,
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
    bool HotkeysEnabled { get; set; }

    IReadOnlyList<HotkeyBindingSetting> GetBindings();

    IReadOnlyList<HotkeyBindingSetting> GetDefaultBindings();

    (bool Success, string Error) SaveBindings(IReadOnlyList<HotkeyBindingSetting> bindings);

    bool TryMatch(Key key, ModifierKeys modifiers, out ShellHotkeyAction action);
}

/// <summary>
/// Default hotkey settings service backed by app settings.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private readonly ISettingsService _settings;
    private readonly object _sync = new();
    private List<HotkeyBindingSetting>? _cachedBindings;

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
            settings.HotkeysEnabled = value;
            _settings.Save(settings);
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

        return (true, string.Empty);
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
}