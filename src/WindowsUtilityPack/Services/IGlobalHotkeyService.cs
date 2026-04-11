using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace WindowsUtilityPack.Services;

/// <summary>
/// A registered global hotkey entry.
/// </summary>
public sealed class GlobalHotkeyRegistration
{
    public required int Id { get; init; }

    public required ShellHotkeyAction Action { get; init; }

    public required Key Key { get; init; }

    public required ModifierKeys Modifiers { get; init; }

    public string DisplayGesture => $"{Modifiers}+{Key}";
}

/// <summary>
/// Diagnostic information for failed global hotkey registrations.
/// </summary>
public sealed class HotkeyRegistrationIssue
{
    public required string Action { get; init; }

    public required string Gesture { get; init; }

    public required string Message { get; init; }
}

/// <summary>
/// Dispatches globally registered shell hotkeys.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler<ShellHotkeyAction>? HotkeyPressed;

    event EventHandler? RegistrationsChanged;

    bool IsStarted { get; }

    IReadOnlyList<GlobalHotkeyRegistration> ActiveRegistrations { get; }

    IReadOnlyList<HotkeyRegistrationIssue> RegistrationIssues { get; }

    void Start();

    void Stop();

    void Refresh();
}

/// <summary>
/// Win32 RegisterHotKey-backed global hotkey service.
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;
    private readonly IHotkeyService _hotkeySettings;
    private readonly ILoggingService? _logging;
    private readonly IGlobalHotkeyNativeApi _nativeApi;
    private readonly Dictionary<int, GlobalHotkeyRegistration> _registrations = [];
    private readonly List<HotkeyRegistrationIssue> _issues = [];
    private HwndSource? _messageSource;
    private bool _started;
    private int _nextRegistrationId = 1;

    public event EventHandler<ShellHotkeyAction>? HotkeyPressed;

    public event EventHandler? RegistrationsChanged;

    public bool IsStarted => _started;

    public IReadOnlyList<GlobalHotkeyRegistration> ActiveRegistrations => _registrations.Values.ToList();

    public IReadOnlyList<HotkeyRegistrationIssue> RegistrationIssues => _issues;

    public GlobalHotkeyService(IHotkeyService hotkeySettings, ILoggingService? logging = null)
        : this(hotkeySettings, logging, new GlobalHotkeyNativeApi())
    {
    }

    internal GlobalHotkeyService(IHotkeyService hotkeySettings, ILoggingService? logging, IGlobalHotkeyNativeApi nativeApi)
    {
        _hotkeySettings = hotkeySettings ?? throw new ArgumentNullException(nameof(hotkeySettings));
        _logging = logging;
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        _hotkeySettings.BindingsChanged += OnBindingsChanged;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        EnsureMessageSink();
        _started = true;
        Refresh();
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        UnregisterAll();
        _started = false;
        RegistrationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Refresh()
    {
        if (!_started)
        {
            return;
        }

        EnsureMessageSink();
        UnregisterAll();
        _issues.Clear();
        _nextRegistrationId = 1;

        if (!_hotkeySettings.HotkeysEnabled || _messageSource is null)
        {
            RegistrationsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        foreach (var binding in _hotkeySettings.GetBindings().Where(static b => b.Enabled))
        {
            if (!Enum.TryParse<ShellHotkeyAction>(binding.Action, out var action))
            {
                _issues.Add(new HotkeyRegistrationIssue
                {
                    Action = binding.Action,
                    Gesture = binding.Gesture,
                    Message = "Unknown action",
                });
                continue;
            }

            if (!TryParseGesture(binding.Gesture, out var key, out var modifiers))
            {
                _issues.Add(new HotkeyRegistrationIssue
                {
                    Action = action.ToString(),
                    Gesture = binding.Gesture,
                    Message = "Invalid gesture format",
                });
                continue;
            }

            var id = _nextRegistrationId++;
            var ok = _nativeApi.RegisterHotKey(_messageSource.Handle, id, ToNativeModifiers(modifiers), (uint)KeyInterop.VirtualKeyFromKey(key));
            if (!ok)
            {
                var error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                _logging?.LogError($"Global hotkey registration failed for {action} ({binding.Gesture}): {error}");
                _issues.Add(new HotkeyRegistrationIssue
                {
                    Action = action.ToString(),
                    Gesture = binding.Gesture,
                    Message = $"Registration failed: {error}",
                });
                continue;
            }

            _registrations[id] = new GlobalHotkeyRegistration
            {
                Id = id,
                Action = action,
                Key = key,
                Modifiers = modifiers,
            };
        }

        RegistrationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _hotkeySettings.BindingsChanged -= OnBindingsChanged;
        Stop();
        if (_messageSource is not null)
        {
            _messageSource.RemoveHook(WndProc);
            _messageSource.Dispose();
            _messageSource = null;
        }
    }

    private void EnsureMessageSink()
    {
        if (_messageSource is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("WindowsUtilityPack.HotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x800000), // WS_POPUP
        };
        _messageSource = new HwndSource(parameters);
        _messageSource.AddHook(WndProc);
    }

    private void OnBindingsChanged(object? sender, EventArgs e) => Refresh();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        var id = wParam.ToInt32();
        if (_registrations.TryGetValue(id, out var registration))
        {
            HotkeyPressed?.Invoke(this, registration.Action);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void UnregisterAll()
    {
        if (_messageSource is null)
        {
            return;
        }

        foreach (var registration in _registrations.Values)
        {
            _nativeApi.UnregisterHotKey(_messageSource.Handle, registration.Id);
        }

        _registrations.Clear();
    }

    internal static bool TryParseGesture(string gestureText, out Key key, out ModifierKeys modifiers)
    {
        key = Key.None;
        modifiers = ModifierKeys.None;
        try
        {
            var converter = new KeyGestureConverter();
            var converted = converter.ConvertFromString(gestureText);
            if (converted is not KeyGesture parsed)
            {
                return false;
            }

            key = parsed.Key;
            modifiers = parsed.Modifiers;
            return key != Key.None;
        }
        catch
        {
            return false;
        }
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        const uint ModAlt = 0x0001;
        const uint ModControl = 0x0002;
        const uint ModShift = 0x0004;
        const uint ModWin = 0x0008;

        var value = 0u;
        if (modifiers.HasFlag(ModifierKeys.Alt)) value |= ModAlt;
        if (modifiers.HasFlag(ModifierKeys.Control)) value |= ModControl;
        if (modifiers.HasFlag(ModifierKeys.Shift)) value |= ModShift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) value |= ModWin;
        return value;
    }
}

internal interface IGlobalHotkeyNativeApi
{
    bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    bool UnregisterHotKey(IntPtr windowHandle, int id);
}

internal sealed class GlobalHotkeyNativeApi : IGlobalHotkeyNativeApi
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool NativeRegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool NativeUnregisterHotKey(IntPtr hWnd, int id);

    public bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey)
        => NativeRegisterHotKey(windowHandle, id, modifiers, virtualKey);

    public bool UnregisterHotKey(IntPtr windowHandle, int id)
        => NativeUnregisterHotKey(windowHandle, id);
}
