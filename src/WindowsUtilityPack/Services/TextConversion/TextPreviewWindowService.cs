using WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;

namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Default modeless preview-window manager.
/// </summary>
public sealed class TextPreviewWindowService : ITextPreviewWindowService
{
    private readonly ITextPreviewWindowHostFactory _hostFactory;
    private ITextPreviewWindowHost? _currentHost;

    public TextPreviewWindowService()
        : this(new TextPreviewWindowHostFactory())
    {
    }

    public TextPreviewWindowService(ITextPreviewWindowHostFactory hostFactory)
    {
        _hostFactory = hostFactory;
    }

    /// <inheritdoc />
    public bool HasOpenPreview => _currentHost is { IsVisible: true };

    /// <inheritdoc />
    public void ShowOrUpdate(TextPreviewWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (_currentHost is null || !_currentHost.IsVisible)
        {
            _currentHost = _hostFactory.Create(viewModel);
            _currentHost.Closed += OnHostClosed;
            _currentHost.Show();
            return;
        }

        _currentHost.DataContext = viewModel;
        _currentHost.Activate();
    }

    private void OnHostClosed(object? sender, EventArgs e)
    {
        if (_currentHost is not null)
        {
            _currentHost.Closed -= OnHostClosed;
            _currentHost = null;
        }
    }
}

/// <summary>
/// Default factory for modeless preview window hosts.
/// </summary>
public sealed class TextPreviewWindowHostFactory : ITextPreviewWindowHostFactory
{
    /// <inheritdoc />
    public ITextPreviewWindowHost Create(TextPreviewWindowViewModel viewModel)
    {
        return new TextPreviewWindow
        {
            DataContext = viewModel,
        };
    }
}
