using System.Windows;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.ViewModels;
using WindowsUtilityPack.Views;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Hosts the detached command palette window.
/// </summary>
public interface ICommandPaletteHostService
{
    event EventHandler<CommandPaletteItem>? CommandInvoked;

    bool IsOpen { get; }

    void ShowOrActivate();

    void Close();
}

public interface ICommandPaletteWindowHost
{
    event EventHandler? Closed;

    bool IsVisible { get; }

    CommandPaletteWindowViewModel ViewModel { get; }

    void Show();

    void Activate();

    void Close();
}

public interface ICommandPaletteWindowHostFactory
{
    ICommandPaletteWindowHost Create(CommandPaletteWindowViewModel viewModel);
}

/// <summary>
/// Default command palette host service.
/// </summary>
public sealed class CommandPaletteHostService : ICommandPaletteHostService
{
    private readonly ICommandPaletteService _paletteService;
    private readonly ICommandPaletteWindowHostFactory _windowFactory;
    private ICommandPaletteWindowHost? _windowHost;
    private CommandPaletteWindowViewModel? _viewModel;

    public event EventHandler<CommandPaletteItem>? CommandInvoked;

    public bool IsOpen => _windowHost?.IsVisible == true;

    public CommandPaletteHostService(ICommandPaletteService paletteService)
        : this(paletteService, new CommandPaletteWindowHostFactory())
    {
    }

    internal CommandPaletteHostService(ICommandPaletteService paletteService, ICommandPaletteWindowHostFactory windowFactory)
    {
        _paletteService = paletteService ?? throw new ArgumentNullException(nameof(paletteService));
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
    }

    public void ShowOrActivate()
    {
        if (_windowHost is not null && _windowHost.IsVisible)
        {
            _windowHost.ViewModel.ActivateFresh();
            _windowHost.Activate();
            return;
        }

        _viewModel = new CommandPaletteWindowViewModel(_paletteService);
        _viewModel.ExecuteRequested += OnExecuteRequested;
        _windowHost = _windowFactory.Create(_viewModel);
        _windowHost.Closed += OnWindowClosed;
        _windowHost.Show();
    }

    public void Close()
    {
        _windowHost?.Close();
    }

    private void OnExecuteRequested(object? sender, CommandPaletteItem item)
    {
        CommandInvoked?.Invoke(this, item);
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_windowHost is not null)
        {
            _windowHost.Closed -= OnWindowClosed;
        }

        if (_viewModel is not null)
        {
            _viewModel.ExecuteRequested -= OnExecuteRequested;
        }

        _windowHost = null;
        _viewModel = null;
    }
}

public sealed class CommandPaletteWindowHostFactory : ICommandPaletteWindowHostFactory
{
    public ICommandPaletteWindowHost Create(CommandPaletteWindowViewModel viewModel)
    {
        var window = new CommandPaletteWindow
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow,
            ShowInTaskbar = false,
            Topmost = true,
        };

        return new CommandPaletteWindowHost(window, viewModel);
    }
}

internal sealed class CommandPaletteWindowHost : ICommandPaletteWindowHost
{
    private readonly CommandPaletteWindow _window;

    public CommandPaletteWindowHost(CommandPaletteWindow window, CommandPaletteWindowViewModel viewModel)
    {
        _window = window;
        ViewModel = viewModel;
        _window.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Closed;

    public bool IsVisible => _window.IsVisible;

    public CommandPaletteWindowViewModel ViewModel { get; }

    public void Show()
    {
        ViewModel.ActivateFresh();
        _window.Show();
        _window.Activate();
    }

    public void Activate()
    {
        _window.Activate();
    }

    public void Close()
    {
        _window.Close();
    }
}
