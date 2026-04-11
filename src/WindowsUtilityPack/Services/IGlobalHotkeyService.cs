using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Event data for a globally-triggered hotkey.
/// </summary>
public sealed class GlobalHotkeyEventArgs : EventArgs
{
    /// <summary>The ID that was passed to <see cref="IGlobalHotkeyService.TryRegister"/>.</summary>
    public int HotkeyId { get; }

    /// <summary>The modifier keys that are part of this binding.</summary>
    public ModifierKeys Modifiers { get; }

    /// <summary>The primary key of this binding.</summary>
    public Key Key { get; }

    public GlobalHotkeyEventArgs(int id, ModifierKeys modifiers, Key key)
    {
        HotkeyId = id;
        Modifiers = modifiers;
        Key = key;
    }
}

/// <summary>
/// Manages system-wide (global) hotkey registration so features work even
/// when the main window is minimised to tray.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>
    /// Raised on the UI thread whenever a registered global hotkey is activated.
    /// </summary>
    event EventHandler<GlobalHotkeyEventArgs>? HotkeyTriggered;

    /// <summary>
    /// Attaches the Win32 WM_HOTKEY message hook to the WPF UI thread.
    /// Must be called once from the UI thread (e.g., in <c>OnSourceInitialized</c>).
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    void Attach();

    /// <summary>
    /// Attempts to register a global hotkey with the given <paramref name="id"/>.
    /// If registration fails (e.g., conflict with another app) the error is returned
    /// rather than thrown.
    /// </summary>
    (bool Success, string? Error) TryRegister(int id, ModifierKeys modifiers, Key key);

    /// <summary>
    /// Unregisters the hotkey with the given <paramref name="id"/>.
    /// Returns <see langword="true"/> if it was registered.
    /// </summary>
    bool Unregister(int id);

    /// <summary>
    /// Unregisters all currently registered hotkeys.
    /// </summary>
    void UnregisterAll();

    /// <summary>
    /// Returns <see langword="true"/> if the hotkey with the given ID is currently registered.
    /// </summary>
    bool IsRegistered(int id);

    /// <summary>Number of currently active global hotkey registrations.</summary>
    int RegisteredCount { get; }

    /// <summary>
    /// Clears all existing registrations and registers every enabled binding
    /// from <paramref name="hotkeyService"/>.
    /// Returns the number of successfully registered bindings and any error messages.
    /// </summary>
    (int Registered, IReadOnlyList<string> Errors) SyncFromHotkeyService(IHotkeyService hotkeyService);
}

/// <summary>
/// Production implementation of <see cref="IGlobalHotkeyService"/> using Win32
/// <c>RegisterHotKey</c> / <c>UnregisterHotKey</c> and
/// <see cref="ComponentDispatcher.ThreadPreprocessMessage"/> for message interception.
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    // WM_HOTKEY message constant.
    private const int WmHotkey = 0x0312;

    // Win32 modifier flag constants.
    private const uint ModAlt        = 0x0001;
    private const uint ModControl    = 0x0002;
    private const uint ModShift      = 0x0004;
    private const uint ModWin        = 0x0008;
    private const uint ModNoRepeat   = 0x4000;

    // Registered hotkeys: id → (modifiers, key).
    private readonly Dictionary<int, (ModifierKeys Modifiers, Key Key)> _registered = new();
    private readonly object _lock = new();
    private bool _attached;
    private bool _disposed;

    public event EventHandler<GlobalHotkeyEventArgs>? HotkeyTriggered;

    public int RegisteredCount
    {
        get
        {
            lock (_lock)
            {
                return _registered.Count;
            }
        }
    }

    /// <inheritdoc/>
    public void Attach()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_attached)
        {
            return;
        }

        ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
        _attached = true;
    }

    /// <inheritdoc/>
    public (bool Success, string? Error) TryRegister(int id, ModifierKeys modifiers, Key key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_registered.ContainsKey(id))
            {
                // Unregister existing before re-registering.
                NativeUnregisterHotKey(nint.Zero, id);
                _registered.Remove(id);
            }
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        var mods = ToWin32Modifiers(modifiers);

        if (!NativeRegisterHotKey(nint.Zero, id, mods, vk))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return (false, $"RegisterHotKey failed for id={id}: {error}");
        }

        lock (_lock)
        {
            _registered[id] = (modifiers, key);
        }

        return (true, null);
    }

    /// <inheritdoc/>
    public bool Unregister(int id)
    {
        lock (_lock)
        {
            if (!_registered.ContainsKey(id))
            {
                return false;
            }

            NativeUnregisterHotKey(nint.Zero, id);
            _registered.Remove(id);
            return true;
        }
    }

    /// <inheritdoc/>
    public void UnregisterAll()
    {
        lock (_lock)
        {
            foreach (var id in _registered.Keys)
            {
                NativeUnregisterHotKey(nint.Zero, id);
            }

            _registered.Clear();
        }
    }

    /// <inheritdoc/>
    public bool IsRegistered(int id)
    {
        lock (_lock)
        {
            return _registered.ContainsKey(id);
        }
    }

    /// <inheritdoc/>
    public (int Registered, IReadOnlyList<string> Errors) SyncFromHotkeyService(IHotkeyService hotkeyService)
    {
        ArgumentNullException.ThrowIfNull(hotkeyService);

        UnregisterAll();

        if (!hotkeyService.HotkeysEnabled)
        {
            return (0, []);
        }

        var registeredCount = 0;
        var errors = new List<string>();

        foreach (var binding in hotkeyService.GetBindings())
        {
            if (!binding.Enabled || string.IsNullOrWhiteSpace(binding.Gesture))
            {
                continue;
            }

            if (!Enum.TryParse<ShellHotkeyAction>(binding.Action, out var action))
            {
                errors.Add($"Unknown action: {binding.Action}");
                continue;
            }

            if (!TryParseGesture(binding.Gesture, out var key, out var mods))
            {
                errors.Add($"Unparseable gesture: {binding.Gesture}");
                continue;
            }

            var id = (int)action + 1; // IDs must be > 0 per Win32 spec.
            var (success, error) = TryRegister(id, mods, key);
            if (success)
            {
                registeredCount++;
            }
            else
            {
                errors.Add(error ?? $"Failed to register {binding.Action}");
            }
        }

        return (registeredCount, errors);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_attached)
        {
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
            _attached = false;
        }

        UnregisterAll();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (handled || msg.message != WmHotkey)
        {
            return;
        }

        var id = (int)(nint)msg.wParam;
        (ModifierKeys modifiers, Key key) registration;

        lock (_lock)
        {
            if (!_registered.TryGetValue(id, out registration))
            {
                return;
            }
        }

        handled = true;
        HotkeyTriggered?.Invoke(this, new GlobalHotkeyEventArgs(id, registration.Modifiers, registration.Key));
    }

    private static uint ToWin32Modifiers(ModifierKeys modifiers)
    {
        uint flags = ModNoRepeat;
        if ((modifiers & ModifierKeys.Alt) != 0)     flags |= ModAlt;
        if ((modifiers & ModifierKeys.Control) != 0) flags |= ModControl;
        if ((modifiers & ModifierKeys.Shift) != 0)   flags |= ModShift;
        if ((modifiers & ModifierKeys.Windows) != 0) flags |= ModWin;
        return flags;
    }

    private static bool TryParseGesture(string gestureText, out Key key, out ModifierKeys modifiers)
    {
        key = Key.None;
        modifiers = ModifierKeys.None;

        try
        {
            var converter = new KeyGestureConverter();
            var converted = converter.ConvertFromString(gestureText);
            if (converted is not KeyGesture gesture)
            {
                return false;
            }

            key = gesture.Key;
            modifiers = gesture.Modifiers;
            return true;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    // Wrapper aliases to avoid shadowing the interface methods.
    private static bool NativeRegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk)
        => RegisterHotKey(hWnd, id, fsModifiers, vk);

    private static bool NativeUnregisterHotKey(nint hWnd, int id)
        => UnregisterHotKey(hWnd, id);
}
