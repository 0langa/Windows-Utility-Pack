using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.ImageTools;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.ImageTools.ScreenshotAnnotator;

public sealed class AnnotationRow : ViewModelBase
{
    private string _type = "Rectangle";
    private float _x;
    private float _y;
    private float _width = 200;
    private float _height = 120;
    private string _text = string.Empty;
    private string _color = "#FF3B30";
    private float _thickness = 3;

    public string Type { get => _type; set => SetProperty(ref _type, value); }
    public float X { get => _x; set => SetProperty(ref _x, value); }
    public float Y { get => _y; set => SetProperty(ref _y, value); }
    public float Width { get => _width; set => SetProperty(ref _width, Math.Max(0, value)); }
    public float Height { get => _height; set => SetProperty(ref _height, Math.Max(0, value)); }
    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public string Color { get => _color; set => SetProperty(ref _color, value); }
    public float Thickness { get => _thickness; set => SetProperty(ref _thickness, Math.Clamp(value, 1, 20)); }
}

/// <summary>
/// ViewModel for screenshot capture and basic annotation workflows.
/// </summary>
public sealed class ScreenshotAnnotatorViewModel : ViewModelBase
{
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IClipboardService _clipboardService;

    private string _imagePath = string.Empty;
    private string _outputPath = string.Empty;
    private BitmapImage? _previewImage;
    private string _annotationType = "Rectangle";
    private float _x = 100;
    private float _y = 100;
    private float _width = 240;
    private float _height = 120;
    private string _annotationText = "Note";
    private string _annotationColor = "#FF3B30";
    private float _annotationThickness = 3;
    private bool _isBusy;
    private string _statusMessage = "Capture or load an image, add annotations, and save the result.";
    private AnnotationRow? _selectedAnnotation;

    public ObservableCollection<string> AnnotationTypes { get; } = ["Rectangle", "Arrow", "Text", "Redaction", "Blur"];
    public ObservableCollection<AnnotationRow> Annotations { get; } = [];

    public string ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public BitmapImage? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    public string AnnotationType
    {
        get => _annotationType;
        set => SetProperty(ref _annotationType, value);
    }

    public float X
    {
        get => _x;
        set => SetProperty(ref _x, Math.Max(0, value));
    }

    public float Y
    {
        get => _y;
        set => SetProperty(ref _y, Math.Max(0, value));
    }

    public float Width
    {
        get => _width;
        set => SetProperty(ref _width, Math.Max(0, value));
    }

    public float Height
    {
        get => _height;
        set => SetProperty(ref _height, Math.Max(0, value));
    }

    public string AnnotationText
    {
        get => _annotationText;
        set => SetProperty(ref _annotationText, value);
    }

    public string AnnotationColor
    {
        get => _annotationColor;
        set => SetProperty(ref _annotationColor, value);
    }

    public float AnnotationThickness
    {
        get => _annotationThickness;
        set => SetProperty(ref _annotationThickness, Math.Clamp(value, 1, 20));
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

    public AnnotationRow? SelectedAnnotation
    {
        get => _selectedAnnotation;
        set
        {
            if (!SetProperty(ref _selectedAnnotation, value))
            {
                return;
            }

            if (value is not null)
            {
                AnnotationType = value.Type;
                X = value.X;
                Y = value.Y;
                Width = value.Width;
                Height = value.Height;
                AnnotationText = value.Text;
                AnnotationColor = value.Color;
                AnnotationThickness = value.Thickness;
            }

            RelayCommand.RaiseCanExecuteChanged();
        }
    }

    public AsyncRelayCommand CaptureScreenshotCommand { get; }
    public RelayCommand LoadImageCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand AddAnnotationCommand { get; }
    public RelayCommand ApplyEditorToSelectedCommand { get; }
    public RelayCommand DuplicateSelectedAnnotationCommand { get; }
    public RelayCommand RemoveSelectedAnnotationCommand { get; }
    public RelayCommand ClearAnnotationsCommand { get; }
    public AsyncRelayCommand SaveAnnotatedImageCommand { get; }
    public RelayCommand CopyOutputPathCommand { get; }
    public RelayCommand OpenOutputFolderCommand { get; }

    public ScreenshotAnnotatorViewModel(IImageProcessingService imageProcessingService, IClipboardService clipboardService)
    {
        _imageProcessingService = imageProcessingService;
        _clipboardService = clipboardService;

        CaptureScreenshotCommand = new AsyncRelayCommand(_ => CaptureScreenshotAsync(), _ => !IsBusy);
        LoadImageCommand = new RelayCommand(_ => LoadImageFromDisk());
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutputPath());
        AddAnnotationCommand = new RelayCommand(_ => AddAnnotation());
        ApplyEditorToSelectedCommand = new RelayCommand(_ => ApplyEditorToSelected(), _ => SelectedAnnotation is not null);
        DuplicateSelectedAnnotationCommand = new RelayCommand(_ => DuplicateSelected(), _ => SelectedAnnotation is not null);
        RemoveSelectedAnnotationCommand = new RelayCommand(_ => RemoveSelected(), _ => SelectedAnnotation is not null);
        ClearAnnotationsCommand = new RelayCommand(_ => ClearAllAnnotations(), _ => Annotations.Count > 0);
        SaveAnnotatedImageCommand = new AsyncRelayCommand(_ => SaveAnnotatedAsync(), _ => !IsBusy);
        CopyOutputPathCommand = new RelayCommand(_ => _clipboardService.SetText(OutputPath), _ => !string.IsNullOrWhiteSpace(OutputPath));
        OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder(), _ => !string.IsNullOrWhiteSpace(OutputPath));
    }

    private async Task CaptureScreenshotAsync()
    {
        IsBusy = true;
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(desktop, $"screenshot_{timestamp}.png");
            var result = await _imageProcessingService.CaptureScreenshotAsync(path, CancellationToken.None);
            if (!result.Success)
            {
                StatusMessage = $"Screenshot failed: {result.ErrorMessage}";
                return;
            }

            ImagePath = result.ImagePath;
            if (string.IsNullOrWhiteSpace(OutputPath))
                OutputPath = Path.Combine(desktop, $"screenshot_annotated_{timestamp}.png");
            LoadPreview(ImagePath);
            StatusMessage = $"Screenshot captured: {result.Width}x{result.Height}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Screenshot failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadImageFromDisk()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp|All files|*.*",
        };

        if (dialog.ShowDialog() != true)
            return;

        ImagePath = dialog.FileName;
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            var dir = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(dialog.FileName);
            OutputPath = Path.Combine(dir, $"{name}_annotated.png");
        }

        LoadPreview(ImagePath);
        StatusMessage = "Image loaded.";
    }

    private void BrowseOutputPath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save annotated image",
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|WEBP (*.webp)|*.webp|BMP (*.bmp)|*.bmp|TIFF (*.tiff)|*.tiff",
            FileName = string.IsNullOrWhiteSpace(OutputPath) ? "annotated.png" : Path.GetFileName(OutputPath),
            InitialDirectory = string.IsNullOrWhiteSpace(OutputPath) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : Path.GetDirectoryName(OutputPath),
        };

        if (dialog.ShowDialog() == true)
            OutputPath = dialog.FileName;
    }

    private void AddAnnotation()
    {
        var annotation = new AnnotationRow
        {
            Type = AnnotationType,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            Text = AnnotationText,
            Color = AnnotationColor,
            Thickness = AnnotationThickness,
        };
        Annotations.Add(annotation);
        SelectedAnnotation = annotation;

        StatusMessage = $"Added {AnnotationType} annotation.";
        RelayCommand.RaiseCanExecuteChanged();
    }

    private void RemoveSelected()
    {
        if (SelectedAnnotation is null)
            return;
        Annotations.Remove(SelectedAnnotation);
        SelectedAnnotation = null;
        StatusMessage = "Selected annotation removed.";
        RelayCommand.RaiseCanExecuteChanged();
    }

    private void ApplyEditorToSelected()
    {
        if (SelectedAnnotation is null)
        {
            return;
        }

        SelectedAnnotation.Type = AnnotationType;
        SelectedAnnotation.X = X;
        SelectedAnnotation.Y = Y;
        SelectedAnnotation.Width = Width;
        SelectedAnnotation.Height = Height;
        SelectedAnnotation.Text = AnnotationText;
        SelectedAnnotation.Color = AnnotationColor;
        SelectedAnnotation.Thickness = AnnotationThickness;
        StatusMessage = "Applied editor values to selected annotation.";
    }

    private void DuplicateSelected()
    {
        if (SelectedAnnotation is null)
        {
            return;
        }

        var duplicate = new AnnotationRow
        {
            Type = SelectedAnnotation.Type,
            X = SelectedAnnotation.X + 12,
            Y = SelectedAnnotation.Y + 12,
            Width = SelectedAnnotation.Width,
            Height = SelectedAnnotation.Height,
            Text = SelectedAnnotation.Text,
            Color = SelectedAnnotation.Color,
            Thickness = SelectedAnnotation.Thickness,
        };

        Annotations.Add(duplicate);
        SelectedAnnotation = duplicate;
        StatusMessage = "Duplicated selected annotation.";
    }

    private void ClearAllAnnotations()
    {
        Annotations.Clear();
        SelectedAnnotation = null;
        StatusMessage = "Cleared all annotations.";
        RelayCommand.RaiseCanExecuteChanged();
    }

    private async Task SaveAnnotatedAsync()
    {
        if (string.IsNullOrWhiteSpace(ImagePath) || !File.Exists(ImagePath))
        {
            StatusMessage = "Please capture or load an image first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusMessage = "Please choose an output path.";
            return;
        }

        IsBusy = true;
        try
        {
            var annotations = Annotations.Select(ToImageAnnotation).ToList();
            var format = ParseFormatFromPath(OutputPath);
            var result = await _imageProcessingService.AnnotateAsync(
                ImagePath,
                OutputPath,
                annotations,
                format,
                quality: 90,
                CancellationToken.None);

            if (!result.Success)
            {
                StatusMessage = $"Save failed: {result.ErrorMessage}";
                return;
            }

            LoadPreview(OutputPath);
            StatusMessage = $"Annotated image saved: {OutputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static ImageAnnotation ToImageAnnotation(AnnotationRow row)
    {
        return new ImageAnnotation
        {
            Type = row.Type.ToUpperInvariant() switch
            {
                "ARROW" => Services.ImageTools.AnnotationType.Arrow,
                "TEXT" => Services.ImageTools.AnnotationType.Text,
                "REDACTION" => Services.ImageTools.AnnotationType.Redaction,
                "BLUR" => Services.ImageTools.AnnotationType.Blur,
                _ => Services.ImageTools.AnnotationType.Rectangle,
            },
            X = row.X,
            Y = row.Y,
            Width = row.Width,
            Height = row.Height,
            Text = row.Text,
            ColorHex = row.Color,
            StrokeThickness = row.Thickness,
        };
    }

    private static ImageOutputFormat ParseFormatFromPath(string outputPath)
    {
        return Path.GetExtension(outputPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageOutputFormat.Jpeg,
            ".webp" => ImageOutputFormat.Webp,
            ".bmp" => ImageOutputFormat.Bmp,
            ".tif" or ".tiff" => ImageOutputFormat.Tiff,
            _ => ImageOutputFormat.Png,
        };
    }

    private void LoadPreview(string path)
    {
        if (!File.Exists(path))
            return;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        PreviewImage = bitmap;
    }

    private void OpenOutputFolder()
    {
        try
        {
            var folder = Path.GetDirectoryName(OutputPath);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                StatusMessage = "Output folder does not exist.";
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open output folder: {ex.Message}";
        }
    }
}
