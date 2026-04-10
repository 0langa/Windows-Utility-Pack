using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.ImageTools;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.ImageTools.ImageFormatConverter;

public sealed class ImageConverterResultRow : ViewModelBase
{
    private bool _success;
    private string _input = string.Empty;
    private string _output = string.Empty;
    private string _message = string.Empty;

    public bool Success { get => _success; set => SetProperty(ref _success, value); }
    public string Input { get => _input; set => SetProperty(ref _input, value); }
    public string Output { get => _output; set => SetProperty(ref _output, value); }
    public string Message { get => _message; set => SetProperty(ref _message, value); }
}

/// <summary>
/// ViewModel for image format conversion workflows.
/// </summary>
public sealed class ImageFormatConverterViewModel : ViewModelBase
{
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IClipboardService _clipboardService;

    private CancellationTokenSource? _operationCts;
    private string _outputDirectory = string.Empty;
    private string _targetFormat = "PNG";
    private int _quality = 90;
    private bool _overwriteExisting;
    private bool _isBusy;
    private string _statusMessage = "Add image files and choose a target format.";
    private string? _selectedInputFile;

    public ObservableCollection<string> InputFiles { get; } = [];
    public ObservableCollection<string> TargetFormats { get; } = ["JPEG", "PNG", "WEBP", "BMP", "TIFF"];
    public ObservableCollection<ImageConverterResultRow> Results { get; } = [];

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetProperty(ref _outputDirectory, value);
    }

    public string TargetFormat
    {
        get => _targetFormat;
        set => SetProperty(ref _targetFormat, value);
    }

    public int Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, Math.Clamp(value, 1, 100));
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

    public string? SelectedInputFile
    {
        get => _selectedInputFile;
        set => SetProperty(ref _selectedInputFile, value);
    }

    public RelayCommand AddFilesCommand { get; }
    public RelayCommand RemoveSelectedFileCommand { get; }
    public RelayCommand ClearFilesCommand { get; }
    public RelayCommand BrowseOutputDirectoryCommand { get; }
    public AsyncRelayCommand ConvertCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand CopySummaryCommand { get; }

    public ImageFormatConverterViewModel(
        IImageProcessingService imageProcessingService,
        IFolderPickerService folderPickerService,
        IClipboardService clipboardService)
    {
        _imageProcessingService = imageProcessingService;
        _folderPickerService = folderPickerService;
        _clipboardService = clipboardService;

        AddFilesCommand = new RelayCommand(_ => AddFiles());
        RemoveSelectedFileCommand = new RelayCommand(_ => RemoveSelected(), _ => SelectedInputFile is not null);
        ClearFilesCommand = new RelayCommand(_ => InputFiles.Clear(), _ => InputFiles.Count > 0);
        BrowseOutputDirectoryCommand = new RelayCommand(_ => BrowseOutputDirectory());
        ConvertCommand = new AsyncRelayCommand(_ => ConvertAsync(), _ => !IsBusy);
        CancelCommand = new RelayCommand(_ => _operationCts?.Cancel(), _ => IsBusy);
        CopySummaryCommand = new RelayCommand(_ => CopySummary(), _ => Results.Count > 0);
    }

    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Select files for conversion",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp;*.heic|All files|*.*",
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

    private void RemoveSelected()
    {
        if (SelectedInputFile is null)
            return;
        InputFiles.Remove(SelectedInputFile);
        SelectedInputFile = null;
    }

    private void BrowseOutputDirectory()
    {
        var selected = _folderPickerService.PickFolder("Select image conversion output folder");
        if (!string.IsNullOrWhiteSpace(selected))
            OutputDirectory = selected;
    }

    private async Task ConvertAsync()
    {
        if (InputFiles.Count == 0)
        {
            StatusMessage = "Please add image files first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            StatusMessage = "Please select an output directory.";
            return;
        }

        IsBusy = true;
        Results.Clear();
        _operationCts = new CancellationTokenSource();
        StatusMessage = "Converting images...";

        try
        {
            var request = new ImageConvertRequest
            {
                InputPaths = InputFiles.ToList(),
                OutputDirectory = OutputDirectory,
                OutputFormat = ParseFormat(TargetFormat),
                Quality = Quality,
                Overwrite = OverwriteExisting,
            };

            var results = await _imageProcessingService.ConvertAsync(request, _operationCts.Token);
            foreach (var result in results)
            {
                Results.Add(new ImageConverterResultRow
                {
                    Success = result.Success,
                    Input = Path.GetFileName(result.InputPath),
                    Output = result.OutputPath,
                    Message = result.Success
                        ? $"{FormatBytes(result.InputBytes)} -> {FormatBytes(result.OutputBytes)}"
                        : result.ErrorMessage,
                });
            }

            StatusMessage = $"Completed: {results.Count(r => r.Success)}/{results.Count} converted.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Conversion cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Conversion failed: {ex.Message}";
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
            ? $"OK  {r.Input} -> {r.Output} ({r.Message})"
            : $"ERR {r.Input}: {r.Message}");
        _clipboardService.SetText(string.Join(Environment.NewLine, lines));
        StatusMessage = "Summary copied to clipboard.";
    }

    private static ImageOutputFormat ParseFormat(string format)
    {
        return format.ToUpperInvariant() switch
        {
            "JPEG" => ImageOutputFormat.Jpeg,
            "WEBP" => ImageOutputFormat.Webp,
            "BMP" => ImageOutputFormat.Bmp,
            "TIFF" => ImageOutputFormat.Tiff,
            _ => ImageOutputFormat.Png,
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:F1} KB";
        return $"{bytes / (1024d * 1024d):F2} MB";
    }
}
