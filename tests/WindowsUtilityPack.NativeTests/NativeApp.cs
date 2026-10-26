using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace WindowsUtilityPack.NativeTests;

// This assembly deliberately has no reference to the application. All product
// interactions cross the process boundary through Windows UI Automation/input.
internal sealed class NativeApp : IDisposable
{
    internal static string? EvidenceDirectory { get; set; }
    internal static List<object> Actions { get; } = [];
    private static void Record(string action, string control, string? value = null) => Actions.Add(new { Utc = DateTimeOffset.UtcNow, Action = action, Control = control, Value = value?.Length > 2000 ? value[..2000] : value });
    internal Process Process { get; }
    internal AutomationElement? TargetWindow { get; set; }
    internal AutomationElement Window => TargetWindow ?? AutomationElement.FromHandle(Process.MainWindowHandle);
    internal NativeApp(string executable, string? profile)
    {
        if (profile != null)
        {
        Directory.CreateDirectory(profile);
        var settings = Path.Combine(profile, "settings.json");
        if (!File.Exists(settings)) File.WriteAllText(settings,
            "{\"RememberWindowPosition\":false,\"HotkeysEnabled\":false,\"TrayModeEnabled\":false,\"CloseToTray\":false,\"ClipboardMonitoringEnabled\":false,\"ClipboardMonitoringConsentAccepted\":false}");
        }
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        if (profile != null) { start.ArgumentList.Add("--profile-directory"); start.ArgumentList.Add(profile); }
        Process = Process.Start(start) ?? throw new InvalidOperationException("Cannot launch published app.");
        Wait(() => { Process.Refresh(); return !Process.HasExited && Process.MainWindowHandle != 0; }, "main window", 45);
        Process.WaitForInputIdle(10000);
        SetForegroundWindow(Process.MainWindowHandle);
    }

    internal static void Wait(Func<bool> predicate, string description, int seconds = 12)
    {
        var watch = Stopwatch.StartNew(); Exception? last = null;
        while (watch.Elapsed.TotalSeconds < seconds)
        {
            try { if (predicate()) return; } catch (ElementNotAvailableException ex) { last = ex; }
            Thread.Sleep(80);
        }
        throw new TimeoutException($"Timed out waiting for {description}.", last);
    }

    private static IEnumerable<AutomationElement> ScopedFind(AutomationElement root, Condition condition)
    {
        foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, condition))
        {
            if (root.Current.ControlType != ControlType.Window) { yield return element; continue; }
            var owner = TreeWalker.ControlViewWalker.GetParent(element);
            while (owner != null && owner.Current.ControlType != ControlType.Window) owner = TreeWalker.ControlViewWalker.GetParent(owner);
            if (owner != null && owner.GetRuntimeId().SequenceEqual(root.GetRuntimeId())) yield return element;
        }
    }
    internal AutomationElement? Find(string id, AutomationElement? root = null) =>
        ScopedFind(root ?? Window, new PropertyCondition(AutomationElement.AutomationIdProperty, id)).FirstOrDefault();
    internal AutomationElement Get(string id, AutomationElement? root = null)
    {
        if (root == null) Wait(() => Window.Current.IsEnabled, "main window interactive (close its modal dialog first)");
        AutomationElement? found = null;
        Wait(() => (found = Find(id, root)) != null, $"control {id}"); return found!;
    }
    internal AutomationElement Named(string name, ControlType? type = null, AutomationElement? root = null)
    {
        if (root == null) Wait(() => Window.Current.IsEnabled, "main window interactive (close its modal dialog first)");
        Condition condition = new PropertyCondition(AutomationElement.NameProperty, name);
        if (type != null) condition = new AndCondition(condition, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
        AutomationElement? found = null;
        Wait(() => (found = ScopedFind(root ?? Window, condition).FirstOrDefault()) != null, $"control named {name}");
        return found!;
    }
    internal static string Read(AutomationElement el) => el.Current.IsPassword ? "[password]" : el.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
        ? ((ValuePattern)pattern).Current.Value : el.TryGetCurrentPattern(RangeValuePattern.Pattern, out var range)
        ? ((RangeValuePattern)range).Current.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : el.Current.Name;
    internal string Text(string id) => Read(Get(id));
    internal string[][] GridRows(string id)
    {
        var grid = (GridPattern)Get(id).GetCurrentPattern(GridPattern.Pattern);
        var rows = Enumerable.Range(0, grid.Current.RowCount).Select(row =>
            Enumerable.Range(0, grid.Current.ColumnCount).Select(column =>
            {
                var cell = grid.GetItem(row, column);
                var checkbox = cell.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox));
                return checkbox?.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle) == true
                    ? (((TogglePattern)toggle).Current.ToggleState == ToggleState.On).ToString()
                    : Read(cell);
            }).ToArray()).ToArray();
        Record("read-grid", id, System.Text.Json.JsonSerializer.Serialize(rows));
        return rows;
    }
    internal void Set(string id, string value)
    {
        Get(id);
        var el = ScopedFind(Window, new PropertyCondition(AutomationElement.AutomationIdProperty, id))
            .FirstOrDefault(e => e.TryGetCurrentPattern(ValuePattern.Pattern, out var p) && !((ValuePattern)p).Current.IsReadOnly)
            ?? throw new InvalidOperationException($"No editable control for {id}");
        ((ValuePattern)el.GetCurrentPattern(ValuePattern.Pattern)).SetValue(value);
        Wait(() => el.Current.IsPassword || Read(el) == value, $"{id} edit");
        Record("set", id, el.Current.IsPassword ? "[fixture password]" : value);
    }
    internal void Click(string id) => Invoke(Get(id));
    internal static void Invoke(AutomationElement el)
    {
        if (!el.Current.IsEnabled) throw new InvalidOperationException($"Disabled control: {el.Current.AutomationId} / {el.Current.Name}");
        Record("invoke", el.Current.AutomationId, el.Current.Name);
        // Invoke may wait for modal dialogs to close in some native providers.
        _ = Task.Run(() => ((InvokePattern)el.GetCurrentPattern(InvokePattern.Pattern)).Invoke());
        Thread.Sleep(100);
    }
    internal void Button(string name) => Invoke(Named(name, ControlType.Button));
    internal void Key(string key, AutomationElement? target = null)
    {
        SetForegroundWindow(target == null ? Process.MainWindowHandle : (IntPtr)target.Current.NativeWindowHandle);
        if (target != null) target.SetFocus();
        System.Windows.Forms.SendKeys.SendWait(key);
    }
    internal void Navigate(string title)
    {
        Button("Open command palette");
        Set("CommandPaletteQueryBox", title);
        var results = Named("Command palette results", ControlType.List);
        var label = Named(title, ControlType.Text, results);
        var item = label;
        while (item != null && item.Current.ControlType != ControlType.ListItem)
            item = TreeWalker.ControlViewWalker.GetParent(item);
        if (item == null) throw new InvalidOperationException($"Palette result has no selectable item: {title}");
        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Get("CommandPaletteQueryBox").SetFocus(); Key("{ENTER}");
        Wait(() => Find("CommandPaletteQueryBox") == null, $"navigation to {title}");
        Process.WaitForInputIdle(10000);
        Thread.Sleep(160);
    }
    internal void Select(string id, string item)
    {
        var combo = Get(id);
        ((ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
        AutomationElement? option = null;
        Wait(() => {
            option = combo.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, item));
            while (option != null && option.Current.ControlType != ControlType.ListItem) option = TreeWalker.ControlViewWalker.GetParent(option);
            return option != null;
        }, $"combo option {item}");
        ((SelectionItemPattern)option!.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        ((ExpandCollapsePattern)combo.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Collapse();
        Record("select", id, item);
    }
    internal void Check(string id, bool value)
    {
        var toggle = (TogglePattern)Get(id).GetCurrentPattern(TogglePattern.Pattern);
        if ((toggle.Current.ToggleState == ToggleState.On) != value)
        {
            // A toggle can open a consent dialog; do not block the thread needed to answer it.
            _ = Task.Run(toggle.Toggle);
            Thread.Sleep(100);
        }
        Record("toggle", id, value.ToString());
    }
    internal void Row(string id, string text)
    {
        var grid = Get(id);
        AutomationElement? cell = null;
        Wait(() => (cell = grid.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, text))) != null, $"row {text}");
        var row = cell;
        while (row != null && !row.TryGetCurrentPattern(SelectionItemPattern.Pattern, out _)) row = TreeWalker.ControlViewWalker.GetParent(row);
        if (row == null) throw new InvalidOperationException($"No selectable row for {text}");
        ((SelectionItemPattern)row.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    }
    internal void Tab(string name) => ((SelectionItemPattern)Named(name, ControlType.TabItem).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    internal void Expect(string id, Func<string, bool> condition, string description, int seconds = 15)
    {
        Wait(() => condition(Text(id)), description, seconds);
        Record("assert: " + description, id, Text(id));
    }

    internal AutomationElement Dialog()
    {
        AutomationElement? dialog = null;
        Wait(() => {
            var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, Process.Id));
            dialog = windows.Cast<AutomationElement>().FirstOrDefault(w => w.Current.NativeWindowHandle != Process.MainWindowHandle && !w.Current.IsOffscreen && !string.IsNullOrWhiteSpace(w.Current.Name));
            dialog ??= Window.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
            return dialog != null;
        }, "native dialog");
        return dialog!;
    }
    internal void FileDialog(string path, bool folder = false)
    {
        var dialog = Dialog();
        AutomationElement? Edit(string id) => dialog.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, id), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
        AutomationElement Accept() => dialog.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
            ?? throw new InvalidOperationException("Native dialog accept button missing.");
        var edit = Edit(folder ? "1152" : "1148") ?? Edit("1001")
            ?? throw new InvalidOperationException("Native filename control missing.");
        if (EvidenceDirectory != null) File.WriteAllText(Path.Combine(EvidenceDirectory, $"dialog-{DateTime.UtcNow:HHmmssfff}.txt"), DumpElement(dialog));
        SetForegroundWindow((IntPtr)dialog.Current.NativeWindowHandle);
        edit.SetFocus();
        System.Windows.Forms.Clipboard.SetText(path);
        System.Windows.Forms.SendKeys.SendWait("^a"); System.Windows.Forms.SendKeys.SendWait("^v");
        Wait(() => Read(edit) == path, "native dialog filename input");
        Invoke(Accept());
        if (folder)
        {
            // The shell may first navigate to a typed absolute directory. A
            // second confirmation then selects that current directory.
            try
            {
                Wait(() => {
                    try { return dialog.Current.IsOffscreen || dialog.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar)).Cast<AutomationElement>().Any(e => e.Current.Name.EndsWith(path, StringComparison.OrdinalIgnoreCase)); }
                    catch (ElementNotAvailableException) { return true; }
                }, "folder picker navigation");
                if (!dialog.Current.IsOffscreen)
                {
                    var currentEdit = Edit("1152");
                    if (currentEdit != null) ((ValuePattern)currentEdit.GetCurrentPattern(ValuePattern.Pattern)).SetValue("");
                    Invoke(Accept());
                }
            }
            catch (ElementNotAvailableException) { }
        }
        Wait(() => { try { return !dialog.Current.IsEnabled || dialog.Current.IsOffscreen; } catch (ElementNotAvailableException) { return true; } }, "file dialog closed");
    }
    internal void Confirm(string name = "Yes")
    {
        var dialog = Dialog();
        var id = name switch { "Yes" => "6", "No" => "7", "OK" => "1", "Cancel" => "2", _ => "" };
        Invoke(Find(id, dialog) ?? Named(name, ControlType.Button, dialog));
    }
    internal string Dump(AutomationElement? root = null)
        => DumpElement(root ?? Window);
    internal void SaveScreenshot(string path)
    {
        var bounds = Window.Current.BoundingRectangle;
        using var bitmap = new System.Drawing.Bitmap((int)bounds.Width, (int)bounds.Height);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen((int)bounds.X, (int)bounds.Y, 0, 0, bitmap.Size);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
    private static string DumpElement(AutomationElement root)
    {
        var text = new StringBuilder();
        foreach (var el in root.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>().Take(2500))
        {
            try { text.AppendLine($"{el.Current.ControlType.ProgrammaticName} | {el.Current.AutomationId} | {el.Current.Name} | {Read(el)} | enabled={el.Current.IsEnabled} offscreen={el.Current.IsOffscreen}"); }
            catch (ElementNotAvailableException) { }
        }
        return text.ToString();
    }
    public void Dispose()
    {
        if (!Process.HasExited)
        {
            Process.CloseMainWindow();
            if (!Process.WaitForExit(6000)) Process.Kill(entireProcessTree: true);
        }
        Process.Dispose();
    }
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    internal static void ClickAt(int x, int y)
    {
        if (Environment.UserName != "WDAGUtilityAccount") throw new InvalidOperationException("Pixel fixture input is guest-only.");
        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
        mouse_event(2, 0, 0, 0, UIntPtr.Zero); mouse_event(4, 0, 0, 0, UIntPtr.Zero);
    }
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    internal static string? ReadClipboardText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var handle = GetClipboardData(13); // CF_UNICODETEXT, read actual Windows clipboard storage
            if (handle == IntPtr.Zero) return string.Empty;
            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(pointer) ?? string.Empty; }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr handle);
}
