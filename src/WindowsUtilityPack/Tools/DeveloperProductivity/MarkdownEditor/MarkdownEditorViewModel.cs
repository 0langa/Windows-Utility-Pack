using System.IO;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.MarkdownEditor;

/// <summary>
/// ViewModel for markdown editing and preview rendering.
/// </summary>
public sealed class MarkdownEditorViewModel : ViewModelBase
{
    private readonly IMarkdownEditorService _service;
    private readonly IClipboardService _clipboard;
    private readonly IUserDialogService _dialogs;

    private string _filePath = string.Empty;
    private string _markdownText = string.Empty;
    private string _renderedHtml = string.Empty;
    private string _statusMessage = "Create or open a markdown document.";
    private bool _isBusy;
    private int _lineCount;
    private int _wordCount;
    private int _characterCount;

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public string MarkdownText
    {
        get => _markdownText;
        set
        {
            if (SetProperty(ref _markdownText, value))
            {
                RefreshStats();
            }
        }
    }

    public string RenderedHtml
    {
        get => _renderedHtml;
        set => SetProperty(ref _renderedHtml, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public int LineCount
    {
        get => _lineCount;
        set => SetProperty(ref _lineCount, value);
    }

    public int WordCount
    {
        get => _wordCount;
        set => SetProperty(ref _wordCount, value);
    }

    public int CharacterCount
    {
        get => _characterCount;
        set => SetProperty(ref _characterCount, value);
    }

    public RelayCommand NewDocumentCommand { get; }
    public AsyncRelayCommand OpenCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand SaveAsCommand { get; }
    public RelayCommand RenderCommand { get; }
    public RelayCommand CopyHtmlCommand { get; }

    public MarkdownEditorViewModel(IMarkdownEditorService service, IClipboardService clipboard, IUserDialogService dialogs)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        NewDocumentCommand = new RelayCommand(_ => NewDocument());
        OpenCommand = new AsyncRelayCommand(_ => OpenAsync());
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        SaveAsCommand = new AsyncRelayCommand(_ => SaveAsAsync());
        RenderCommand = new RelayCommand(_ => Render());
        CopyHtmlCommand = new RelayCommand(_ => CopyHtml());

        RefreshStats();
    }

    internal void Render()
    {
        RenderedHtml = _service.RenderHtml(MarkdownText);
        StatusMessage = "Markdown rendered to HTML preview.";
    }

    internal async Task OpenAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open markdown file",
            Filter = "Markdown files (*.md;*.markdown)|*.md;*.markdown|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        try
        {
            FilePath = dialog.FileName;
            MarkdownText = await _service.LoadAsync(FilePath).ConfigureAwait(true);
            Render();
            StatusMessage = $"Loaded {Path.GetFileName(FilePath)}.";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Markdown Editor", ex.Message);
            StatusMessage = "Unable to open markdown file.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            await SaveAsAsync().ConfigureAwait(true);
            return;
        }

        IsBusy = true;
        try
        {
            await _service.SaveAsync(FilePath, MarkdownText).ConfigureAwait(true);
            StatusMessage = $"Saved {Path.GetFileName(FilePath)}.";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Markdown Editor", ex.Message);
            StatusMessage = "Unable to save markdown file.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task SaveAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save markdown file",
            Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(FilePath) ? "notes.md" : Path.GetFileName(FilePath),
            DefaultExt = ".md",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        FilePath = dialog.FileName;
        await SaveAsync().ConfigureAwait(true);
    }

    private void NewDocument()
    {
        FilePath = string.Empty;
        MarkdownText = string.Empty;
        RenderedHtml = string.Empty;
        StatusMessage = "New markdown document created.";
    }

    private void CopyHtml()
    {
        _clipboard.SetText(RenderedHtml);
        StatusMessage = "Rendered HTML copied to clipboard.";
    }

    private void RefreshStats()
    {
        var stats = _service.GetStats(MarkdownText);
        LineCount = stats.LineCount;
        WordCount = stats.WordCount;
        CharacterCount = stats.CharacterCount;
    }
}