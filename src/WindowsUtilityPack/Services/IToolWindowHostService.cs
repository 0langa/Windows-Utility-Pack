using System.Windows;
using System.Windows.Controls;
using WindowsUtilityPack.Tools;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Opens registered tools in detached modeless windows.
/// </summary>
public interface IToolWindowHostService
{
    bool TryOpenOrActivate(string toolKey, out string message);

    int OpenWindowCount { get; }

    void CloseAll();
}

/// <summary>
/// Default detached tool window host service.
/// </summary>
public sealed class ToolWindowHostService : IToolWindowHostService
{
    private readonly IToolWindowHostFactory _factory;
    private readonly Dictionary<string, IToolWindowHost> _openWindows = new(StringComparer.OrdinalIgnoreCase);

    public ToolWindowHostService()
        : this(new ToolWindowHostFactory())
    {
    }

    public ToolWindowHostService(IToolWindowHostFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public int OpenWindowCount => _openWindows.Count;

    public bool TryOpenOrActivate(string toolKey, out string message)
    {
        if (string.IsNullOrWhiteSpace(toolKey))
        {
            message = "Tool key is required.";
            return false;
        }

        if (_openWindows.TryGetValue(toolKey, out var existing) && existing.IsVisible)
        {
            existing.Activate();
            message = "Tool window activated.";
            return true;
        }

        var tool = ToolRegistry.GetByKey(toolKey);
        if (tool is null)
        {
            message = "Tool is not registered.";
            return false;
        }

        var viewModel = tool.Factory();
        var host = _factory.Create(tool, viewModel);
        host.Closed += (_, _) => _openWindows.Remove(tool.Key);
        _openWindows[tool.Key] = host;
        host.Show();

        message = $"Opened '{tool.Name}' in a detached window.";
        return true;
    }

    public void CloseAll()
    {
        foreach (var host in _openWindows.Values.ToList())
        {
            host.Close();
        }

        _openWindows.Clear();
    }
}

/// <summary>
/// Abstraction for a detached tool window host.
/// </summary>
public interface IToolWindowHost
{
    event EventHandler? Closed;

    bool IsVisible { get; }

    void Show();

    void Activate();

    void Close();
}

/// <summary>
/// Factory for detached tool windows.
/// </summary>
public interface IToolWindowHostFactory
{
    IToolWindowHost Create(Models.ToolDefinition tool, object viewModel);
}

/// <summary>
/// Default WPF implementation of detached tool window hosts.
/// </summary>
public sealed class ToolWindowHostFactory : IToolWindowHostFactory
{
    public IToolWindowHost Create(Models.ToolDefinition tool, object viewModel)
    {
        var window = new Window
        {
            Title = $"{tool.Name} - Windows Utility Pack",
            Width = 980,
            Height = 700,
            MinWidth = 760,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ContentControl { Content = viewModel },
            Background = Application.Current.MainWindow?.Background,
        };

        if (Application.Current.MainWindow is { IsLoaded: true } owner)
        {
            window.Owner = owner;
        }

        return new ToolWindowHost(window);
    }
}

internal sealed class ToolWindowHost : IToolWindowHost
{
    private readonly Window _window;

    public ToolWindowHost(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _window.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Closed;

    public bool IsVisible => _window.IsVisible;

    public void Show() => _window.Show();

    public void Activate() => _window.Activate();

    public void Close() => _window.Close();
}