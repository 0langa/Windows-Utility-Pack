using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.ImageTools;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.ImageTools.ImageResizer;

public sealed class ImageResizerResultRow : ViewModelBase
{
    private bool _success;
    private string _fileName = string.Empty;
    private string _outputPath = string.Empty;
    private string _sizeSummary = string.Empty;
    private string _error = string.Empty;

    public bool Success { get => _success; set => SetProperty(ref _success, value); }
    public string FileName { get => _fileName; set => SetProperty(ref _fileName, value); }
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }
    public string SizeSummary { get => _sizeSummary; set => SetProperty(ref _sizeSummary, value); }
    public string Error { get => _error; set => SetProperty(ref _error, value); }
}

/// <summary>
/// ViewModel for Image Resizer & Compressor.
/// </summary>
public sealed class ImageResizerViewModel : ViewModelBase
{
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IClipboardService _clipboardService;

    private CancellationTokenSource? _operationCts;
    private string _outputDirectory = string.Empty;
    private int _width = 1920;
    private int _height = 1080;
    private bool _keepAspectRatio = true;
    private int _quality = 85;
    private string _format = "JPEG";
    private bool _overwriteExisting;
    private bool _isBusy;
    private string _statusMessage = "Add one or more images to resize and compress.";
    private string? _selectedInputFile;

    public ObservableCollection<string> InputFiles { get; } = [];
    public ObservableCollection<ImageResizerResultRow> Results { get; } = [];
    public ObservableCollection<string> Formats { get; } = ["JPEG", "PNG", "WEBP", "BMP", "TIFF"];

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetProperty(ref _outputDirectory, value);
    }

    public int Width
    {
        get => _width;
        set => SetProperty(ref _width, Math.Clamp(value, 1, 20000));
    }

    public int Height
    {
        get => _height;
        set => SetProperty(ref _height, Math.Clamp(value, 1, 20000));
    }

    public bool KeepAspectRatio
    {
        get => _keepAspectRatio;
        set => SetProperty(ref _keepAspectRatio, value);
    }

    public int Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, Math.Clamp(value, 1, 100));
    }

    public string Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    public bool OverwriteExisting
    {
        get => _overwriteExisting;
        set => SetProperty(ref _overwriteExisting, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand AddFilesCommand { get; }
    public RelayCommand RemoveSelectedFileCommand { get; }
    public RelayCommand ClearFilesCommand { get; }
    public RelayCommand BrowseOutputDirectoryCommand { get; }
    public AsyncRelayCommand ResizeCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand CopySummaryCommand { get; }

    public string? SelectedInputFile
    {
        get => _selectedInputFile;
        set => SetProperty(ref _selectedInputFile, value);
    }

    public ImageResizerViewModel(
        IImageProcessingService imageProcessingService,
        IFolderPickerService folderPickerService,
        IClipboardService clipboardService)
    {
        _imageProcessingService = imageProcessingService;
        _folderPickerService = folderPickerService;
        _clipboardService = clipboardService;

        AddFilesCommand = new RelayCommand(_ => AddFiles());
        RemoveSelectedFileCommand = new RelayCommand(_ => RemoveSelectedFile(), _ => SelectedInputFile is not null);
        ClearFilesCommand = new RelayCommand(_ => InputFiles.Clear(), _ => InputFiles.Count > 0);
        BrowseOutputDirectoryCommand = new RelayCommand(_ => BrowseOutputDirectory());
        ResizeCommand = new AsyncRelayCommand(_ => ResizeAsync(), _ => !IsBusy);
        CancelCommand = new RelayCommand(_ => _operationCts?.Cancel(), _ => IsBusy);
        CopySummaryCommand = new RelayCommand(_ => CopySummary(), _ => Results.Count > 0);
    }

    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Select images",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp|All files|*.*",
        };

        if (dialog.ShowDialog() != true)
            return;

        foreach (var file in dialog.FileNames)
        {
            if (!InputFiles.Contains(file))
                InputFiles.Add(file);
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
            OutputDirectory = Path.GetDirectoryName(dialog.FileNames[0]) ?? string.Empty;
    }

    private void RemoveSelectedFile()
    {
        if (SelectedInputFile is null)
            return;

        InputFiles.Remove(SelectedInputFile);
        SelectedInputFile = null;
    }

    private void BrowseOutputDirectory()
    {
        var selected = _folderPickerService.PickFolder("Select image resize output folder");
        if (!string.IsNullOrWhiteSpace(selected))
            OutputDirectory = selected;
    }

    private async Task ResizeAsync()
    {
        if (InputFiles.Count == 0)
        {
            StatusMessage = "Please add at least one image file.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            StatusMessage = "Please select an output directory.";
            return;
        }

        _operationCts = new CancellationTokenSource();
        IsBusy = true;
        Results.Clear();
        StatusMessage = "Processing images...";

        try
        {
            var request = new ImageResizeRequest
            {
                InputPaths = InputFiles.ToList(),
                OutputDirectory = OutputDirectory,
                Width = Width,
                Height = Height,
                KeepAspectRatio = KeepAspectRatio,
                Quality = Quality,
                OutputFormat = ParseFormat(Format),
                Overwrite = OverwriteExisting,
            };

            var results = await _imageProcessingService.ResizeAsync(request, _operationCts.Token);
            foreach (var result in results)
            {
                Results.Add(new ImageResizerResultRow
                {
                    Success = result.Success,
                    FileName = Path.GetFileName(result.InputPath),
                    OutputPath = result.OutputPath,
                    SizeSummary = result.Success
                        ? $"{result.Width}x{result.Height} | {FormatBytes(result.InputBytes)} -> {FormatBytes(result.OutputBytes)}"
                        : string.Empty,
                    Error = result.ErrorMessage,
                });
            }

            var successCount = results.Count(r => r.Success);
            StatusMessage = $"Completed: {successCount}/{results.Count} images processed.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Resize operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Resize failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _operationCts?.Dispose();
            _operationCts = null;
        }
    }

    private void CopySummary()
    {
        var lines = Results.Select(r => r.Success
            ? $"OK  {r.FileName} -> {r.OutputPath} ({r.SizeSummary})"
            : $"ERR {r.FileName}: {r.Error}");

        _clipboardService.SetText(string.Join(Environment.NewLine, lines));
        StatusMessage = "Summary copied to clipboard.";
    }

    private static ImageOutputFormat ParseFormat(string format)
    {
        return format.ToUpperInvariant() switch
        {
            "PNG" => ImageOutputFormat.Png,
            "WEBP" => ImageOutputFormat.Webp,
            "BMP" => ImageOutputFormat.Bmp,
            "TIFF" => ImageOutputFormat.Tiff,
            _ => ImageOutputFormat.Jpeg,
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:F1} KB";
        return $"{bytes / (1024d * 1024d):F2} MB";
    }
}
