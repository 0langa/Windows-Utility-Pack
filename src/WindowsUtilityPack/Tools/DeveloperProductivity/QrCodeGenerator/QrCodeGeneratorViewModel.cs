using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.QrCode;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.QrCodeGenerator;

/// <summary>
/// ViewModel for the QR Code Generator tool.
/// Supports URL normalization, live preview generation, styling, export, and utility actions.
/// </summary>
public sealed class QrCodeGeneratorViewModel : ViewModelBase
{
    private readonly IQrCodeService _qrService;
    private readonly IQrCodeFileDialogService _fileDialog;
    private readonly IClipboardService _clipboard;
    private readonly IUserDialogService _dialogs;
    private readonly ISettingsService _settingsService;

    private CancellationTokenSource? _generateCts;

    private string _urlInput = "https://example.com";
    private string _normalizedUrl = string.Empty;
    private string _statusMessage = "Ready";
    private string _validationMessage = string.Empty;
    private string _foregroundHex = "#111827";
    private string _backgroundHex = "#FFFFFF";
    private string _frameHex = "#374151";
    private string _captionHex = "#1F2937";
    private bool _transparentBackground;
    private bool _autoGenerate = true;
    private bool _includeFrame;
    private bool _includeCaption;
    private bool _includeTimestampInFileName;
    private bool _isBusy;
    private int _sizePixels = 360;
    private int _quietZoneModules = 4;
    private int _frameThickness = 8;
    private int _logoScalePercent = 16;
    private int _logoPaddingPixels = 6;
    private int _previewZoomPercent = 100;
    private int _exportSizePixels = 1024;
    private int _exportDpi = 300;
    private string _captionText = string.Empty;
    private string _logoPath = string.Empty;
    private BitmapSource? _logoImage;
    private BitmapSource? _previewImage;
    private string _previewSvgMarkup = string.Empty;
    private QrCodeStylePreset _selectedPreset = QrCodeStylePreset.Classic;
    private QrCodeErrorCorrectionLevel _selectedErrorCorrection = QrCodeErrorCorrectionLevel.Medium;
    private QrCodeModuleShape _selectedModuleShape = QrCodeModuleShape.Square;
    private QrCodeExportFormat _selectedExportFormat = QrCodeExportFormat.Png;
    private string _lastExportDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    public string UrlInput
    {
        get => _urlInput;
        set
        {
            if (SetProperty(ref _urlInput, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public string NormalizedUrl
    {
        get => _normalizedUrl;
        private set => SetProperty(ref _normalizedUrl, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public string ForegroundHex
    {
        get => _foregroundHex;
        set
        {
            if (SetProperty(ref _foregroundHex, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public string BackgroundHex
    {
        get => _backgroundHex;
        set
        {
            if (SetProperty(ref _backgroundHex, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public string FrameHex
    {
        get => _frameHex;
        set
        {
            if (SetProperty(ref _frameHex, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public string CaptionHex
    {
        get => _captionHex;
        set
        {
            if (SetProperty(ref _captionHex, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public bool TransparentBackground
    {
        get => _transparentBackground;
        set
        {
            if (SetProperty(ref _transparentBackground, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public bool AutoGenerate
    {
        get => _autoGenerate;
        set => SetProperty(ref _autoGenerate, value);
    }

    public bool IncludeFrame
    {
        get => _includeFrame;
        set
        {
            if (SetProperty(ref _includeFrame, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public bool IncludeCaption
    {
        get => _includeCaption;
        set
        {
            if (SetProperty(ref _includeCaption, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public bool IncludeTimestampInFileName
    {
        get => _includeTimestampInFileName;
        set => SetProperty(ref _includeTimestampInFileName, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int SizePixels
    {
        get => _sizePixels;
        set
        {
            if (SetProperty(ref _sizePixels, Math.Clamp(value, 120, 1000)))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public int QuietZoneModules
    {
        get => _quietZoneModules;
        set
        {
            if (SetProperty(ref _quietZoneModules, Math.Clamp(value, 0, 12)))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public int FrameThickness
    {
        get => _frameThickness;
        set
        {
            if (SetProperty(ref _frameThickness, Math.Clamp(value, 0, 48)))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public int LogoScalePercent
    {
        get => _logoScalePercent;
        set
        {
            if (SetProperty(ref _logoScalePercent, Math.Clamp(value, 8, 30)))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public int LogoPaddingPixels
    {
        get => _logoPaddingPixels;
        set
        {
            if (SetProperty(ref _logoPaddingPixels, Math.Clamp(value, 0, 32)))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public int PreviewZoomPercent
    {
        get => _previewZoomPercent;
        set => SetProperty(ref _previewZoomPercent, Math.Clamp(value, 50, 300));
    }

    public int ExportSizePixels
    {
        get => _exportSizePixels;
        set => SetProperty(ref _exportSizePixels, Math.Clamp(value, 256, 4096));
    }

    public int ExportDpi
    {
        get => _exportDpi;
        set => SetProperty(ref _exportDpi, Math.Clamp(value, 72, 600));
    }

    public string CaptionText
    {
        get => _captionText;
        set
        {
            if (SetProperty(ref _captionText, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public string LogoPath
    {
        get => _logoPath;
        private set => SetProperty(ref _logoPath, value);
    }

    public BitmapSource? PreviewImage
    {
        get => _previewImage;
        private set
        {
            if (SetProperty(ref _previewImage, value))
            {
                OnPropertyChanged(nameof(HasPreview));
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string PreviewSvgMarkup
    {
        get => _previewSvgMarkup;
        private set => SetProperty(ref _previewSvgMarkup, value);
    }

    public QrCodeStylePreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                ApplyPreset(value, preserveUrl: true);
            }
        }
    }

    public QrCodeErrorCorrectionLevel SelectedErrorCorrection
    {
        get => _selectedErrorCorrection;
        set
        {
            if (SetProperty(ref _selectedErrorCorrection, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public QrCodeModuleShape SelectedModuleShape
    {
        get => _selectedModuleShape;
        set
        {
            if (SetProperty(ref _selectedModuleShape, value))
            {
                ScheduleAutoGenerate();
            }
        }
    }

    public QrCodeExportFormat SelectedExportFormat
    {
        get => _selectedExportFormat;
        set => SetProperty(ref _selectedExportFormat, value);
    }

    public bool HasPreview => PreviewImage is not null;

    public bool HasLogo => _logoImage is not null;

    public IReadOnlyList<QrCodeStylePreset> Presets { get; } =
    [
        QrCodeStylePreset.Classic,
        QrCodeStylePreset.DarkMinimal,
        QrCodeStylePreset.BrandAccent,
        QrCodeStylePreset.Transparent,
        QrCodeStylePreset.PrintFriendly,
    ];

    public IReadOnlyList<QrCodeErrorCorrectionLevel> ErrorCorrectionLevels { get; } =
    [
        QrCodeErrorCorrectionLevel.Low,
        QrCodeErrorCorrectionLevel.Medium,
        QrCodeErrorCorrectionLevel.Quartile,
        QrCodeErrorCorrectionLevel.High,
    ];

    public IReadOnlyList<QrCodeModuleShape> ModuleShapes { get; } =
    [
        QrCodeModuleShape.Square,
        QrCodeModuleShape.Rounded,
    ];

    public IReadOnlyList<QrCodeExportFormat> ExportFormats { get; } =
    [
        QrCodeExportFormat.Png,
        QrCodeExportFormat.Jpeg,
        QrCodeExportFormat.Bmp,
        QrCodeExportFormat.Svg,
        QrCodeExportFormat.Pdf,
    ];

    public ObservableCollection<string> Warnings { get; } = [];

    public ObservableCollection<string> RecentUrls { get; } = [];

    public AsyncRelayCommand GenerateCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CopyImageCommand { get; }

    public RelayCommand CopyUrlCommand { get; }

    public RelayCommand OpenUrlCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand ResetDefaultsCommand { get; }

    public RelayCommand BrowseLogoCommand { get; }

    public RelayCommand RemoveLogoCommand { get; }

    public RelayCommand UseRecentCommand { get; }

    public QrCodeGeneratorViewModel(
        IQrCodeService qrService,
        IQrCodeFileDialogService fileDialog,
        IClipboardService clipboard,
        IUserDialogService dialogs,
        ISettingsService settingsService)
    {
        _qrService = qrService;
        _fileDialog = fileDialog;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _settingsService = settingsService;

        GenerateCommand = new AsyncRelayCommand(_ => GenerateNowAsync(userInitiated: true), _ => !IsBusy);
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => HasPreview && !IsBusy);
        CopyImageCommand = new RelayCommand(_ => CopyImage(), _ => HasPreview);
        CopyUrlCommand = new RelayCommand(_ => CopyUrl(), _ => !string.IsNullOrWhiteSpace(NormalizedUrl));
        OpenUrlCommand = new RelayCommand(_ => OpenUrl(), _ => !string.IsNullOrWhiteSpace(NormalizedUrl));
        ClearCommand = new RelayCommand(_ => ClearSession());
        ResetDefaultsCommand = new RelayCommand(_ => ResetDefaults());
        BrowseLogoCommand = new RelayCommand(_ => BrowseLogo());
        RemoveLogoCommand = new RelayCommand(_ => RemoveLogo(), _ => HasLogo);
        UseRecentCommand = new RelayCommand(url => UseRecent(url as string), _ => !IsBusy);

        LoadSettings();
        _ = GenerateNowAsync(userInitiated: false);
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        _includeTimestampInFileName = settings.QrCodeIncludeTimestampInFileName;
        _lastExportDirectory = string.IsNullOrWhiteSpace(settings.QrCodeLastExportDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : settings.QrCodeLastExportDirectory;

        foreach (var item in settings.QrCodeRecentUrls.Take(15))
        {
            RecentUrls.Add(item);
        }

        OnPropertyChanged(nameof(IncludeTimestampInFileName));
    }

    private async Task SaveAsync()
    {
        if (!TryGetNormalizedUrl(out var normalized))
        {
            return;
        }

        var suggested = _qrService.BuildSuggestedFileName(normalized, IncludeTimestampInFileName);
        var exportPath = _fileDialog.PickExportPath(suggested, _lastExportDirectory, SelectedExportFormat);
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            return;
        }

        var format = _fileDialog.InferFormat(exportPath, SelectedExportFormat);
        var request = new QrCodeExportRequest
        {
            NormalizedUrl = normalized,
            FilePath = exportPath,
            Format = format,
            ExportSizePixels = ExportSizePixels,
            RasterDpi = ExportDpi,
            Style = BuildStyle(normalized),
        };

        try
        {
            IsBusy = true;
            var result = await _qrService.ExportAsync(request, CancellationToken.None);
            _lastExportDirectory = Path.GetDirectoryName(result.FilePath) ?? _lastExportDirectory;
            StatusMessage = $"Exported {result.Format} to {result.FilePath}";
            PersistSettings();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Export failed", ex.Message);
            StatusMessage = "Export failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GenerateNowAsync(bool userInitiated)
    {
        _generateCts?.Cancel();
        _generateCts?.Dispose();
        _generateCts = new CancellationTokenSource();

        if (!TryGetNormalizedUrl(out var normalized))
        {
            PreviewImage = null;
            PreviewSvgMarkup = string.Empty;
            if (userInitiated)
            {
                StatusMessage = "Fix URL errors to generate QR code.";
            }

            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Generating preview...";
            var result = await _qrService.GeneratePreviewAsync(normalized, BuildStyle(normalized), _generateCts.Token);
            NormalizedUrl = result.NormalizedUrl;
            PreviewImage = result.Image;
            PreviewSvgMarkup = result.SvgMarkup;
            SetWarnings(result.Warnings);
            StatusMessage = "Preview updated.";
            AddRecentUrl(result.NormalizedUrl);
            PersistSettings();
        }
        catch (OperationCanceledException)
        {
            // Expected when options change rapidly.
        }
        catch (Exception ex)
        {
            if (userInitiated)
            {
                _dialogs.ShowError("QR generation failed", ex.Message);
            }

            StatusMessage = "Generation failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryGetNormalizedUrl(out string normalized)
    {
        if (_qrService.TryNormalizeUrl(UrlInput, out normalized, out var error))
        {
            ValidationMessage = string.Empty;
            return true;
        }

        ValidationMessage = error;
        return false;
    }

    private QrCodeStyleOptions BuildStyle(string normalizedUrl)
    {
        var style = new QrCodeStyleOptions
        {
            SizePixels = SizePixels,
            QuietZoneModules = QuietZoneModules,
            ForegroundColor = ParseHexColor(ForegroundHex, Color.FromRgb(17, 24, 39)),
            BackgroundColor = ParseHexColor(BackgroundHex, Colors.White),
            TransparentBackground = TransparentBackground,
            ErrorCorrectionLevel = SelectedErrorCorrection,
            ModuleShape = SelectedModuleShape,
            IncludeFrame = IncludeFrame,
            FrameColor = ParseHexColor(FrameHex, Color.FromRgb(55, 65, 81)),
            FrameThickness = FrameThickness,
            IncludeCaption = IncludeCaption,
            CaptionText = string.IsNullOrWhiteSpace(CaptionText) ? ExtractDomain(normalizedUrl) : CaptionText.Trim(),
            CaptionColor = ParseHexColor(CaptionHex, Color.FromRgb(31, 41, 55)),
            LogoImage = _logoImage,
            LogoScalePercent = LogoScalePercent,
            LogoPaddingPixels = LogoPaddingPixels,
            DomainLabel = ExtractDomain(normalizedUrl),
        };

        return style;
    }

    private void SetWarnings(IReadOnlyList<string> warnings)
    {
        Warnings.Clear();
        foreach (var warning in warnings)
        {
            Warnings.Add(warning);
        }
    }

    private void AddRecentUrl(string url)
    {
        var existing = RecentUrls.FirstOrDefault(item => string.Equals(item, url, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentUrls.Remove(existing);
        }

        RecentUrls.Insert(0, url);
        while (RecentUrls.Count > 15)
        {
            RecentUrls.RemoveAt(RecentUrls.Count - 1);
        }
    }

    private void PersistSettings()
    {
        var settings = _settingsService.Load();
        settings.QrCodeRecentUrls = RecentUrls.ToList();
        settings.QrCodeIncludeTimestampInFileName = IncludeTimestampInFileName;
        settings.QrCodeLastExportDirectory = _lastExportDirectory;
        _settingsService.Save(settings);
    }

    private async void ScheduleAutoGenerate()
    {
        if (!AutoGenerate)
        {
            return;
        }

        try
        {
            await Task.Delay(200);
            await GenerateNowAsync(userInitiated: false);
        }
        catch
        {
            // Keep debounce path resilient.
        }
    }

    private void ApplyPreset(QrCodeStylePreset preset, bool preserveUrl)
    {
        var presetOptions = QrCodePresetCatalog.CreatePreset(preset);
        ForegroundHex = ToHex(presetOptions.ForegroundColor);
        BackgroundHex = ToHex(presetOptions.BackgroundColor);
        TransparentBackground = presetOptions.TransparentBackground;
        SelectedErrorCorrection = presetOptions.ErrorCorrectionLevel;
        SelectedModuleShape = presetOptions.ModuleShape;
        IncludeFrame = presetOptions.IncludeFrame;
        FrameHex = ToHex(presetOptions.FrameColor);
        FrameThickness = presetOptions.FrameThickness;
        IncludeCaption = presetOptions.IncludeCaption;
        QuietZoneModules = presetOptions.QuietZoneModules;

        if (!preserveUrl)
        {
            UrlInput = string.Empty;
        }

        ScheduleAutoGenerate();
    }

    private void ResetDefaults()
    {
        SelectedPreset = QrCodeStylePreset.Classic;
        SizePixels = 360;
        QuietZoneModules = 4;
        FrameThickness = 8;
        LogoScalePercent = 16;
        LogoPaddingPixels = 6;
        ExportSizePixels = 1024;
        ExportDpi = 300;
        PreviewZoomPercent = 100;
        IncludeTimestampInFileName = true;
        RemoveLogo();
        StatusMessage = "Defaults restored.";
    }

    private void CopyImage()
    {
        if (PreviewImage is null)
        {
            return;
        }

        if (_clipboard.TrySetImage(PreviewImage))
        {
            StatusMessage = "QR image copied to clipboard.";
        }
        else
        {
            _dialogs.ShowError("Clipboard error", "Unable to copy image to clipboard right now.");
        }
    }

    private void CopyUrl()
    {
        if (string.IsNullOrWhiteSpace(NormalizedUrl))
        {
            return;
        }

        _clipboard.SetText(NormalizedUrl);
        StatusMessage = "URL copied to clipboard.";
    }

    private void OpenUrl()
    {
        if (string.IsNullOrWhiteSpace(NormalizedUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = NormalizedUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Open URL failed", ex.Message);
        }
    }

    private void ClearSession()
    {
        UrlInput = string.Empty;
        NormalizedUrl = string.Empty;
        PreviewImage = null;
        PreviewSvgMarkup = string.Empty;
        ValidationMessage = string.Empty;
        StatusMessage = "Cleared.";
    }

    private void BrowseLogo()
    {
        var path = _fileDialog.PickLogoImagePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            using var fileStream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = fileStream;
            image.EndInit();
            image.Freeze();

            _logoImage = image;
            LogoPath = path;
            OnPropertyChanged(nameof(HasLogo));
            ScheduleAutoGenerate();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Logo load failed", ex.Message);
        }
    }

    private void RemoveLogo()
    {
        _logoImage = null;
        LogoPath = string.Empty;
        OnPropertyChanged(nameof(HasLogo));
        RelayCommand.RaiseCanExecuteChanged();
        ScheduleAutoGenerate();
    }

    private void UseRecent(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        UrlInput = url;
        _ = GenerateNowAsync(userInitiated: true);
    }

    private static Color ParseHexColor(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        try
        {
            var value = (Color)ColorConverter.ConvertFromString(hex.Trim());
            return value;
        }
        catch
        {
            return fallback;
        }
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string ExtractDomain(string normalizedUrl)
    {
        if (Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return string.Empty;
    }
}
