using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.FileDataTools.MetadataEditor;

public class MetadataItem
{
    public string Key      { get; set; } = string.Empty;
    public string Value    { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class MetadataEditorViewModel : ViewModelBase
{
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };

    private string _filePath     = string.Empty;
    private string _fileType     = string.Empty;
    private bool   _isLoading;
    private bool   _hasMetadata;
    private string _statusMessage = string.Empty;

    public string FilePath
    {
        get => _filePath;
        set
        {
            SetProperty(ref _filePath, value);
            HasFile = !string.IsNullOrEmpty(value);
        }
    }

    private bool _hasFile;
    public bool HasFile
    {
        get => _hasFile;
        private set => SetProperty(ref _hasFile, value);
    }

    public ObservableCollection<MetadataItem> Metadata { get; } = [];

    public string FileType
    {
        get => _fileType;
        private set => SetProperty(ref _fileType, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasMetadata
    {
        get => _hasMetadata;
        private set => SetProperty(ref _hasMetadata, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand BrowseCommand        { get; }
    public AsyncRelayCommand StripMetadataCommand { get; }
    public RelayCommand      CopyAllCommand       { get; }

    private readonly IClipboardService _clipboard;

    public MetadataEditorViewModel(IClipboardService clipboard)
    {
        _clipboard           = clipboard;
        BrowseCommand        = new AsyncRelayCommand(BrowseAsync);
        StripMetadataCommand = new AsyncRelayCommand(StripMetadataAsync);
        CopyAllCommand       = new RelayCommand(CopyAll);
    }

    private async Task BrowseAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select a file to inspect",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif|Audio|*.mp3;*.flac;*.ogg;*.m4a;*.wav|All Files|*.*"
        };

        if (dlg.ShowDialog() != true) return;

        FilePath = dlg.FileName;
        await LoadMetadataAsync(FilePath);
    }

    private async Task LoadMetadataAsync(string path)
    {
        IsLoading = true;
        Metadata.Clear();
        HasMetadata   = false;
        StatusMessage = "Loading metadata…";

        try
        {
            await Task.Run(() =>
            {
                var items = new List<MetadataItem>();
                var info  = new FileInfo(path);
                var ext   = info.Extension.ToLowerInvariant();

                // --- File system metadata (always available) ---
                items.Add(new MetadataItem { Category = "File System", Key = "File Name",       Value = info.Name });
                items.Add(new MetadataItem { Category = "File System", Key = "Full Path",        Value = info.FullName });
                items.Add(new MetadataItem { Category = "File System", Key = "Size",             Value = FormatSize(info.Length) });
                items.Add(new MetadataItem { Category = "File System", Key = "Extension",        Value = info.Extension });
                items.Add(new MetadataItem { Category = "File System", Key = "Created",          Value = info.CreationTime.ToString("yyyy-MM-dd HH:mm:ss") });
                items.Add(new MetadataItem { Category = "File System", Key = "Modified",         Value = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") });
                items.Add(new MetadataItem { Category = "File System", Key = "Last Accessed",    Value = info.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss") });
                items.Add(new MetadataItem { Category = "File System", Key = "Attributes",       Value = info.Attributes.ToString() });
                items.Add(new MetadataItem { Category = "File System", Key = "Is Read-Only",     Value = info.IsReadOnly.ToString() });

                string fileType = "Unknown";

                // --- Image metadata ---
                if (ImageExtensions.Contains(ext))
                {
                    fileType = "Image";
                    try
                    {
                        // Must decode on the calling thread since BitmapDecoder can need UI context for some codecs
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                using var stream = File.OpenRead(path);
                                var decoder = BitmapDecoder.Create(
                                    stream,
                                    BitmapCreateOptions.PreservePixelFormat,
                                    BitmapCacheOption.OnLoad);

                                var frame = decoder.Frames[0];
                                items.Add(new MetadataItem { Category = "Image", Key = "Width",  Value = $"{frame.PixelWidth} px" });
                                items.Add(new MetadataItem { Category = "Image", Key = "Height", Value = $"{frame.PixelHeight} px" });
                                items.Add(new MetadataItem { Category = "Image", Key = "DPI X",  Value = $"{frame.DpiX:F1}" });
                                items.Add(new MetadataItem { Category = "Image", Key = "DPI Y",  Value = $"{frame.DpiY:F1}" });
                                items.Add(new MetadataItem { Category = "Image", Key = "Format", Value = frame.Format.ToString() });

                                // BitmapMetadata is available for JPEG/TIFF
                                if (frame.Metadata is BitmapMetadata bm)
                                {
                                    TryAdd(items, "EXIF", "Title",    bm.Title);
                                    TryAdd(items, "EXIF", "Subject",  bm.Subject);
                                    TryAdd(items, "EXIF", "Comment",  bm.Comment);
                                    TryAdd(items, "EXIF", "Author",   string.Join("; ", bm.Author ?? []));
                                    TryAdd(items, "EXIF", "Keywords", bm.Keywords != null ? string.Join("; ", bm.Keywords) : null);
                                    TryAdd(items, "EXIF", "Camera Manufacturer", bm.CameraManufacturer);
                                    TryAdd(items, "EXIF", "Camera Model",        bm.CameraModel);
                                    TryAdd(items, "EXIF", "Date Taken",          bm.DateTaken);
                                    TryAdd(items, "EXIF", "Copyright",           bm.Copyright);
                                    TryAdd(items, "EXIF", "Rating",              bm.Rating > 0 ? bm.Rating.ToString() : null);
                                    TryAdd(items, "EXIF", "Application Name",    bm.ApplicationName);
                                    TryAdd(items, "EXIF", "Format",              bm.Format);
                                    TryAdd(items, "EXIF", "Location",            bm.Location);
                                }
                            }
                            catch
                            {
                                items.Add(new MetadataItem { Category = "Image", Key = "Note", Value = "Could not read image metadata." });
                            }
                        });
                    }
                    catch { /* Dispatcher unavailable */ }
                }
                else if (ext is ".mp3" or ".flac" or ".ogg" or ".m4a" or ".wav")
                {
                    fileType = "Audio";
                    items.Add(new MetadataItem
                    {
                        Category = "Audio",
                        Key      = "Note",
                        Value    = "Full ID3/audio tag support requires an external library (e.g. TagLib#)."
                    });
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Metadata.Clear();
                    foreach (var item in items)
                        Metadata.Add(item);

                    FileType      = fileType;
                    HasMetadata   = Metadata.Count > 0;
                    StatusMessage = $"Loaded {Metadata.Count} metadata fields.";
                });
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static void TryAdd(List<MetadataItem> list, string category, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            list.Add(new MetadataItem { Category = category, Key = key, Value = value });
    }

    private async Task StripMetadataAsync()
    {
        if (string.IsNullOrEmpty(FilePath)) return;

        var ext = Path.GetExtension(FilePath).ToLowerInvariant();
        if (!ImageExtensions.Contains(ext))
        {
            StatusMessage = "Metadata stripping is only supported for image files in this version.";
            return;
        }

        var saveDir     = Path.GetDirectoryName(FilePath) ?? string.Empty;
        var baseName    = Path.GetFileNameWithoutExtension(FilePath);
        var defaultOut  = Path.Combine(saveDir, $"{baseName}_stripped{ext}");

        var dlg = new SaveFileDialog
        {
            Title      = "Save stripped image",
            FileName   = Path.GetFileName(defaultOut),
            Filter     = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif|All Files|*.*",
            InitialDirectory = saveDir
        };

        if (dlg.ShowDialog() != true) return;

        var outputPath = dlg.FileName;
        IsLoading     = true;
        StatusMessage = "Stripping metadata…";

        try
        {
            await Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    BitmapDecoder decoder;
                    using (var inStream = File.OpenRead(FilePath))
                    {
                        decoder = BitmapDecoder.Create(
                            inStream,
                            BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.OnLoad);
                    }

                    var frame  = decoder.Frames[0];
                    // Copy raw pixels into a new WriteableBitmap (strips metadata)
                    var clean  = new WriteableBitmap(frame);

                    BitmapEncoder encoder = ext switch
                    {
                        ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                        ".png"            => new PngBitmapEncoder(),
                        ".bmp"            => new BmpBitmapEncoder(),
                        ".tiff" or ".tif" => new TiffBitmapEncoder(),
                        _                 => new PngBitmapEncoder()
                    };

                    encoder.Frames.Add(BitmapFrame.Create(clean));

                    using var outStream = File.Create(outputPath);
                    encoder.Save(outStream);
                });
            });

            StatusMessage = $"Stripped image saved to: {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Strip failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CopyAll()
    {
        if (Metadata.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Metadata for: {FilePath}");
        sb.AppendLine();

        var grouped = Metadata.GroupBy(m => m.Category);
        foreach (var group in grouped)
        {
            sb.AppendLine($"[{group.Key}]");
            foreach (var item in group)
                sb.AppendLine($"  {item.Key,-30} {item.Value}");
            sb.AppendLine();
        }

        _clipboard.SetText(sb.ToString());
        StatusMessage = "All metadata copied to clipboard.";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)                   return $"{bytes} B";
        if (bytes < 1024 * 1024)           return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024)   return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
