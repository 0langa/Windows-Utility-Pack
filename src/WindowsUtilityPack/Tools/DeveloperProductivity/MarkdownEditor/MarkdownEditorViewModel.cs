using System.IO;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.MarkdownEditor;

/// <summary>
/// ViewModel for markdown editing and preview rendering.
/// </summary>
public sealed class MarkdownEditorViewModel : ViewModelBase, INavigationGuard
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
    private string _savedText = string.Empty;
    public bool IsDirty => !string.Equals(_savedText, MarkdownText, StringComparison.Ordinal);
    public bool CanNavigateAway() => !IsBusy && (!IsDirty || _dialogs.Confirm("Unsaved Markdown", "Discard unsaved changes?"));

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
                RenderedHtml = string.Empty;
                OnPropertyChanged(nameof(IsDirty));
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
    public AsyncRelayCommand RenderCommand { get; }
    public RelayCommand CopyHtmlCommand { get; }

    public MarkdownEditorViewModel(IMarkdownEditorService service, IClipboardService clipboard, IUserDialogService dialogs)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        NewDocumentCommand = new RelayCommand(_ => NewDocument(), _ => !IsBusy);
        OpenCommand = new AsyncRelayCommand(_ => OpenAsync(), _ => !IsBusy);
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => !IsBusy);
        SaveAsCommand = new AsyncRelayCommand(_ => SaveAsAsync(), _ => !IsBusy);
        RenderCommand = new AsyncRelayCommand(_ => RenderAsync(), _ => !IsBusy);
        CopyHtmlCommand = new RelayCommand(_ => CopyHtml());

        RefreshStats();
    }

    internal async Task RenderAsync()
    {
        var text = MarkdownText;
        IsBusy = true;
        try
        {
            var rendered = await Task.Run(() => _service.RenderHtml(text));
            if (MarkdownText != text) { StatusMessage = "The document changed. Generate HTML again for the current text."; return; }
            RenderedHtml = rendered;
            StatusMessage = "HTML source generated.";
        }
        catch (Exception ex) { RenderedHtml = ""; StatusMessage = $"Could not generate HTML: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    internal async Task OpenAsync()
    {
        if (!CanNavigateAway()) return;
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
            var text = await _service.LoadAsync(dialog.FileName).ConfigureAwait(true);
            FilePath = dialog.FileName;
            MarkdownText = text;
            _savedText = text;
            OnPropertyChanged(nameof(IsDirty));
            await RenderAsync();
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

        await SaveToAsync(FilePath).ConfigureAwait(true);
    }

    internal async Task SaveToAsync(string path)
    {
        IsBusy = true;
        try
        {
            var saved = MarkdownText;
            await _service.SaveAsync(path, saved).ConfigureAwait(true);
            FilePath = path;
            _savedText = saved;
            OnPropertyChanged(nameof(IsDirty));
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

        await SaveToAsync(dialog.FileName).ConfigureAwait(true);
    }

    private void NewDocument()
    {
        if (!CanNavigateAway()) return;
        _savedText = string.Empty;
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
