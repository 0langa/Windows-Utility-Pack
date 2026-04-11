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

    public AnnotationRow Clone()
    {
        return new AnnotationRow
        {
            Type = Type,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            Text = Text,
            Color = Color,
            Thickness = Thickness,
        };
    }
}

/// <summary>
/// ViewModel for screenshot capture and basic annotation workflows.
/// </summary>
public sealed class ScreenshotAnnotatorViewModel : ViewModelBase
{
    public enum ResizeHandle
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    private enum InteractionMode
    {
        None,
        Create,
        Move,
        Resize,
    }

    private readonly IImageProcessingService _imageProcessingService;
    private readonly IClipboardService _clipboardService;
    private readonly IQuickCaptureStateService? _quickCaptureState;

    private string _imagePath = string.Empty;
    private string _outputPath = string.Empty;
    private BitmapImage? _previewImage;
    private double _previewPixelWidth;
    private double _previewPixelHeight;
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
    private AnnotationRow? _dragPreviewAnnotation;
    private bool _isInteractiveAnnotationActive;
    private float _dragStartX;
    private float _dragStartY;
    private float _interactionOriginX;
    private float _interactionOriginY;
    private float _interactionStartLeft;
    private float _interactionStartTop;
    private float _interactionStartWidth;
    private float _interactionStartHeight;
    private InteractionMode _interactionMode;
    private ResizeHandle _resizeHandle;
    private bool _hasUndoSnapshotForInteraction;
    private readonly Stack<AnnotationStateSnapshot> _undoStack = new();
    private readonly Stack<AnnotationStateSnapshot> _redoStack = new();

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

    public double PreviewPixelWidth
    {
        get => _previewPixelWidth;
        private set => SetProperty(ref _previewPixelWidth, value);
    }

    public double PreviewPixelHeight
    {
        get => _previewPixelHeight;
        private set => SetProperty(ref _previewPixelHeight, value);
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

    public AnnotationRow? DragPreviewAnnotation
    {
        get => _dragPreviewAnnotation;
        private set => SetProperty(ref _dragPreviewAnnotation, value);
    }

    public bool IsInteractiveAnnotationActive
    {
        get => _isInteractiveAnnotationActive;
        private set => SetProperty(ref _isInteractiveAnnotationActive, value);
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public bool CanDrawOnPreview => PreviewPixelWidth > 0 && PreviewPixelHeight > 0 && !IsBusy;

    public AsyncRelayCommand CaptureScreenshotCommand { get; }
    public RelayCommand LoadImageCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand AddAnnotationCommand { get; }
    public RelayCommand ApplyEditorToSelectedCommand { get; }
    public RelayCommand DuplicateSelectedAnnotationCommand { get; }
    public RelayCommand RemoveSelectedAnnotationCommand { get; }
    public RelayCommand ClearAnnotationsCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public AsyncRelayCommand SaveAnnotatedImageCommand { get; }
    public RelayCommand CopyOutputPathCommand { get; }
    public RelayCommand OpenOutputFolderCommand { get; }

    public ScreenshotAnnotatorViewModel(
        IImageProcessingService imageProcessingService,
        IClipboardService clipboardService,
        IQuickCaptureStateService? quickCaptureState = null)
    {
        _imageProcessingService = imageProcessingService;
        _clipboardService = clipboardService;
        _quickCaptureState = quickCaptureState;

        CaptureScreenshotCommand = new AsyncRelayCommand(_ => CaptureScreenshotAsync(), _ => !IsBusy);
        LoadImageCommand = new RelayCommand(_ => LoadImageFromDisk());
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutputPath());
        AddAnnotationCommand = new RelayCommand(_ => AddAnnotation());
        ApplyEditorToSelectedCommand = new RelayCommand(_ => ApplyEditorToSelected(), _ => SelectedAnnotation is not null);
        DuplicateSelectedAnnotationCommand = new RelayCommand(_ => DuplicateSelected(), _ => SelectedAnnotation is not null);
        RemoveSelectedAnnotationCommand = new RelayCommand(_ => RemoveSelected(), _ => SelectedAnnotation is not null);
        ClearAnnotationsCommand = new RelayCommand(_ => ClearAllAnnotations(), _ => Annotations.Count > 0);
        UndoCommand = new RelayCommand(_ => Undo(), _ => CanUndo);
        RedoCommand = new RelayCommand(_ => Redo(), _ => CanRedo);
        SaveAnnotatedImageCommand = new AsyncRelayCommand(_ => SaveAnnotatedAsync(), _ => !IsBusy);
        CopyOutputPathCommand = new RelayCommand(_ => _clipboardService.SetText(OutputPath), _ => !string.IsNullOrWhiteSpace(OutputPath));
        OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder(), _ => !string.IsNullOrWhiteSpace(OutputPath));

        TryLoadLastQuickCapture();
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
        PushUndoSnapshot();
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
        PushUndoSnapshot();
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

        PushUndoSnapshot();
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

        PushUndoSnapshot();
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
        if (Annotations.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        Annotations.Clear();
        SelectedAnnotation = null;
        StatusMessage = "Cleared all annotations.";
        RelayCommand.RaiseCanExecuteChanged();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        _redoStack.Push(CreateSnapshot());
        RestoreSnapshot(_undoStack.Pop());
        StatusMessage = "Undid annotation change.";
    }

    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        _undoStack.Push(CreateSnapshot());
        RestoreSnapshot(_redoStack.Pop());
        StatusMessage = "Redid annotation change.";
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
        PreviewPixelWidth = bitmap.PixelWidth;
        PreviewPixelHeight = bitmap.PixelHeight;
        OnPropertyChanged(nameof(CanDrawOnPreview));
    }

    private void TryLoadLastQuickCapture()
    {
        if (_quickCaptureState is null || string.IsNullOrWhiteSpace(_quickCaptureState.LastCapturePath))
        {
            return;
        }

        if (!File.Exists(_quickCaptureState.LastCapturePath))
        {
            return;
        }

        ImagePath = _quickCaptureState.LastCapturePath;
        var directory = Path.GetDirectoryName(ImagePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var name = Path.GetFileNameWithoutExtension(ImagePath);
        OutputPath = Path.Combine(directory, $"{name}_annotated.png");
        LoadPreview(ImagePath);
        StatusMessage = "Loaded latest quick screenshot for annotation.";
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

    private void PushUndoSnapshot()
    {
        _undoStack.Push(CreateSnapshot());
        _redoStack.Clear();
        NotifyHistoryChanged();
    }

    private AnnotationStateSnapshot CreateSnapshot()
    {
        return new AnnotationStateSnapshot(
            Annotations.Select(static annotation => annotation.Clone()).ToList(),
            SelectedAnnotation is null ? -1 : Annotations.IndexOf(SelectedAnnotation));
    }

    private void RestoreSnapshot(AnnotationStateSnapshot snapshot)
    {
        Annotations.Clear();
        foreach (var annotation in snapshot.Annotations.Select(static item => item.Clone()))
        {
            Annotations.Add(annotation);
        }

        SelectedAnnotation = snapshot.SelectedIndex >= 0 && snapshot.SelectedIndex < Annotations.Count
            ? Annotations[snapshot.SelectedIndex]
            : null;

        NotifyHistoryChanged();
        RelayCommand.RaiseCanExecuteChanged();
    }

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        RelayCommand.RaiseCanExecuteChanged();
    }

    public bool BeginInteractiveAnnotation(double x, double y)
    {
        if (!CanDrawOnPreview)
        {
            return false;
        }

        _interactionMode = InteractionMode.Create;
        _hasUndoSnapshotForInteraction = false;
        _dragStartX = ClampToPreview(x, PreviewPixelWidth);
        _dragStartY = ClampToPreview(y, PreviewPixelHeight);
        DragPreviewAnnotation = new AnnotationRow
        {
            Type = AnnotationType,
            X = _dragStartX,
            Y = _dragStartY,
            Width = 0,
            Height = 0,
            Text = AnnotationText,
            Color = AnnotationColor,
            Thickness = AnnotationThickness,
        };
        IsInteractiveAnnotationActive = true;
        StatusMessage = $"Drawing {AnnotationType} annotation...";
        return true;
    }

    public void UpdateInteractiveAnnotation(double x, double y)
    {
        if (!IsInteractiveAnnotationActive || DragPreviewAnnotation is null || _interactionMode != InteractionMode.Create)
        {
            return;
        }

        var currentX = ClampToPreview(x, PreviewPixelWidth);
        var currentY = ClampToPreview(y, PreviewPixelHeight);

        DragPreviewAnnotation.X = Math.Min(_dragStartX, currentX);
        DragPreviewAnnotation.Y = Math.Min(_dragStartY, currentY);
        DragPreviewAnnotation.Width = Math.Abs(currentX - _dragStartX);
        DragPreviewAnnotation.Height = Math.Abs(currentY - _dragStartY);
    }

    public bool CommitInteractiveAnnotation()
    {
        if (!IsInteractiveAnnotationActive || DragPreviewAnnotation is null || _interactionMode != InteractionMode.Create)
        {
            return false;
        }

        var preview = DragPreviewAnnotation.Clone();
        CancelInteractiveAnnotation(resetStatus: false);

        if (preview.Width < 4 && preview.Height < 4)
        {
            StatusMessage = "Annotation drag was too small to add.";
            return false;
        }

        PushUndoSnapshot();
        Annotations.Add(preview);
        SelectedAnnotation = preview;
        StatusMessage = $"Added {preview.Type} annotation from preview drag.";
        RelayCommand.RaiseCanExecuteChanged();
        return true;
    }

    public void CancelInteractiveAnnotation(bool resetStatus = true)
    {
        DragPreviewAnnotation = null;
        IsInteractiveAnnotationActive = false;
        _interactionMode = InteractionMode.None;
        _hasUndoSnapshotForInteraction = false;

        if (resetStatus)
        {
            StatusMessage = "Annotation drag cancelled.";
        }
    }

    private static float ClampToPreview(double value, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return (float)Math.Clamp(value, 0, max);
    }

    public bool BeginMoveSelected(double x, double y)
    {
        if (!CanDrawOnPreview || SelectedAnnotation is null)
        {
            return false;
        }

        var originX = ClampToPreview(x, PreviewPixelWidth);
        var originY = ClampToPreview(y, PreviewPixelHeight);

        EnsureUndoSnapshotForInteraction();
        _interactionMode = InteractionMode.Move;
        _interactionOriginX = originX;
        _interactionOriginY = originY;

        _interactionStartLeft = SelectedAnnotation.X;
        _interactionStartTop = SelectedAnnotation.Y;
        _interactionStartWidth = SelectedAnnotation.Width;
        _interactionStartHeight = SelectedAnnotation.Height;

        IsInteractiveAnnotationActive = true;
        StatusMessage = "Moving annotation...";
        return true;
    }

    public bool BeginResizeSelected(ResizeHandle handle, double x, double y)
    {
        if (!CanDrawOnPreview || SelectedAnnotation is null)
        {
            return false;
        }

        var originX = ClampToPreview(x, PreviewPixelWidth);
        var originY = ClampToPreview(y, PreviewPixelHeight);

        EnsureUndoSnapshotForInteraction();
        _interactionMode = InteractionMode.Resize;
        _resizeHandle = handle;
        _interactionOriginX = originX;
        _interactionOriginY = originY;

        _interactionStartLeft = SelectedAnnotation.X;
        _interactionStartTop = SelectedAnnotation.Y;
        _interactionStartWidth = SelectedAnnotation.Width;
        _interactionStartHeight = SelectedAnnotation.Height;

        IsInteractiveAnnotationActive = true;
        StatusMessage = "Resizing annotation...";
        return true;
    }

    public void UpdateMoveOrResize(double x, double y)
    {
        if (!IsInteractiveAnnotationActive || SelectedAnnotation is null)
        {
            return;
        }

        if (_interactionMode == InteractionMode.Move)
        {
            var currentX = ClampToPreview(x, PreviewPixelWidth);
            var currentY = ClampToPreview(y, PreviewPixelHeight);

            var deltaX = currentX - _interactionOriginX;
            var deltaY = currentY - _interactionOriginY;

            var newLeft = _interactionStartLeft + deltaX;
            var newTop = _interactionStartTop + deltaY;

            SelectedAnnotation.X = (float)Math.Clamp(newLeft, 0, Math.Max(0, PreviewPixelWidth - SelectedAnnotation.Width));
            SelectedAnnotation.Y = (float)Math.Clamp(newTop, 0, Math.Max(0, PreviewPixelHeight - SelectedAnnotation.Height));
            return;
        }

        if (_interactionMode == InteractionMode.Resize)
        {
            var currentX = ClampToPreview(x, PreviewPixelWidth);
            var currentY = ClampToPreview(y, PreviewPixelHeight);

            var minSize = 4f;

            var left = _interactionStartLeft;
            var top = _interactionStartTop;
            var right = _interactionStartLeft + _interactionStartWidth;
            var bottom = _interactionStartTop + _interactionStartHeight;

            switch (_resizeHandle)
            {
                case ResizeHandle.TopLeft:
                    left = Math.Min(currentX, right - minSize);
                    top = Math.Min(currentY, bottom - minSize);
                    break;
                case ResizeHandle.TopRight:
                    right = Math.Max(currentX, left + minSize);
                    top = Math.Min(currentY, bottom - minSize);
                    break;
                case ResizeHandle.BottomLeft:
                    left = Math.Min(currentX, right - minSize);
                    bottom = Math.Max(currentY, top + minSize);
                    break;
                case ResizeHandle.BottomRight:
                    right = Math.Max(currentX, left + minSize);
                    bottom = Math.Max(currentY, top + minSize);
                    break;
            }

            left = Math.Clamp(left, 0, (float)PreviewPixelWidth);
            top = Math.Clamp(top, 0, (float)PreviewPixelHeight);
            right = Math.Clamp(right, 0, (float)PreviewPixelWidth);
            bottom = Math.Clamp(bottom, 0, (float)PreviewPixelHeight);

            var width = Math.Max(minSize, right - left);
            var height = Math.Max(minSize, bottom - top);

            SelectedAnnotation.X = (float)left;
            SelectedAnnotation.Y = (float)top;
            SelectedAnnotation.Width = (float)width;
            SelectedAnnotation.Height = (float)height;
        }
    }

    public void CommitMoveOrResize()
    {
        if (!IsInteractiveAnnotationActive)
        {
            return;
        }

        IsInteractiveAnnotationActive = false;
        _interactionMode = InteractionMode.None;
        _hasUndoSnapshotForInteraction = false;
        StatusMessage = "Annotation updated.";
        RelayCommand.RaiseCanExecuteChanged();
    }

    public void CancelMoveOrResize()
    {
        if (!IsInteractiveAnnotationActive || _interactionMode is not (InteractionMode.Move or InteractionMode.Resize))
        {
            return;
        }

        // Undo snapshot was taken at interaction start, so undo gives a precise revert.
        Undo();
        IsInteractiveAnnotationActive = false;
        _interactionMode = InteractionMode.None;
        _hasUndoSnapshotForInteraction = false;
        StatusMessage = "Annotation change cancelled.";
    }

    public void DeleteSelectedAnnotation()
    {
        if (SelectedAnnotation is null)
        {
            return;
        }

        PushUndoSnapshot();
        var removed = SelectedAnnotation;
        Annotations.Remove(removed);
        SelectedAnnotation = null;
        StatusMessage = "Deleted selected annotation.";
        RelayCommand.RaiseCanExecuteChanged();
    }

    public void NudgeSelectedAnnotation(int deltaX, int deltaY, int step)
    {
        if (SelectedAnnotation is null || !CanDrawOnPreview)
        {
            return;
        }

        step = Math.Clamp(step, 1, 100);
        EnsureUndoSnapshotForInteraction();
        _interactionMode = InteractionMode.Move;

        var newLeft = SelectedAnnotation.X + deltaX * step;
        var newTop = SelectedAnnotation.Y + deltaY * step;

        SelectedAnnotation.X = (float)Math.Clamp(newLeft, 0, Math.Max(0, PreviewPixelWidth - SelectedAnnotation.Width));
        SelectedAnnotation.Y = (float)Math.Clamp(newTop, 0, Math.Max(0, PreviewPixelHeight - SelectedAnnotation.Height));

        CommitMoveOrResize();
    }

    private void EnsureUndoSnapshotForInteraction()
    {
        if (_hasUndoSnapshotForInteraction)
        {
            return;
        }

        PushUndoSnapshot();
        _hasUndoSnapshotForInteraction = true;
    }

    private sealed record AnnotationStateSnapshot(IReadOnlyList<AnnotationRow> Annotations, int SelectedIndex);
}
