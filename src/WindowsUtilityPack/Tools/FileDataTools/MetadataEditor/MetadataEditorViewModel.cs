using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using WindowsUtilityPack.Services.FileTools;
using WindowsUtilityPack.Services.ImageTools;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;
using TagLibFile = TagLib.File;

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

    private static readonly HashSet<string> AudioExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac", ".ogg", ".m4a", ".wav", ".aac", ".wma" };

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
        BrowseCommand        = new AsyncRelayCommand(BrowseAsync, () => !IsLoading);
        StripMetadataCommand = new AsyncRelayCommand(StripMetadataAsync, () => HasFile && !IsLoading);
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
            var loaded = await Task.Run(async () =>
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
                    using var images = await ImageProcessingService.ReadRasterAsync(path, CancellationToken.None);
                    var frame = images[0];
                    items.Add(new() { Category = "Image", Key = "Width", Value = $"{frame.Width} px" });
                    items.Add(new() { Category = "Image", Key = "Height", Value = $"{frame.Height} px" });
                    items.Add(new() { Category = "Image", Key = "Frames", Value = images.Count.ToString() });
                    items.Add(new() { Category = "Image", Key = "Format", Value = frame.Format.ToString() });
                    items.Add(new() { Category = "Image", Key = "Density", Value = frame.Density.ToString() });
                    foreach (var attribute in frame.AttributeNames)
                        TryAdd(items, "Image", attribute, frame.GetAttribute(attribute));
                    var exif = frame.GetExifProfile();
                    if (exif is not null)
                        foreach (var value in exif.Values)
                            TryAdd(items, "EXIF", value.Tag.ToString(), value.ToString());
                }
                else if (AudioExtensions.Contains(ext))
                {
                    fileType = "Audio";
                    try
                    {
                        using var tagFile = TagLibFile.Create(path);
                        var tag = tagFile.Tag;
                        var properties = tagFile.Properties;

                        TryAdd(items, "Audio", "Title", tag.Title);
                        TryAdd(items, "Audio", "Album", tag.Album);
                        TryAdd(items, "Audio", "Album Artist", string.Join("; ", tag.AlbumArtists ?? []));
                        TryAdd(items, "Audio", "Performers", string.Join("; ", tag.Performers ?? []));
                        TryAdd(items, "Audio", "Composers", string.Join("; ", tag.Composers ?? []));
                        TryAdd(items, "Audio", "Genres", string.Join("; ", tag.Genres ?? []));
                        TryAdd(items, "Audio", "Comment", tag.Comment);
                        TryAdd(items, "Audio", "Year", tag.Year > 0 ? tag.Year.ToString() : null);
                        TryAdd(items, "Audio", "Track", tag.Track > 0 ? tag.Track.ToString() : null);
                        TryAdd(items, "Audio", "Duration", properties.Duration.ToString(@"hh\:mm\:ss"));
                        TryAdd(items, "Audio", "Bitrate", properties.AudioBitrate > 0 ? $"{properties.AudioBitrate} kbps" : null);
                        TryAdd(items, "Audio", "Sample Rate", properties.AudioSampleRate > 0 ? $"{properties.AudioSampleRate} Hz" : null);
                        TryAdd(items, "Audio", "Channels", properties.AudioChannels > 0 ? properties.AudioChannels.ToString() : null);
                        TryAdd(items, "Audio", "Media Types", properties.MediaTypes.ToString());
                    }
                    catch (Exception ex)
                    {
                        items.Add(new MetadataItem
                        {
                            Category = "Audio",
                            Key = "Error",
                            Value = $"Could not read audio metadata: {ex.Message}"
                        });
                    }
                }

                return (items, fileType);
            });
            foreach (var item in loaded.items) Metadata.Add(item);
            FileType = loaded.fileType;
            HasMetadata = Metadata.Count > 0;
            StatusMessage = $"Loaded {Metadata.Count} metadata fields.";
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
        if (!ImageExtensions.Contains(ext) && !AudioExtensions.Contains(ext))
        {
            StatusMessage = "Metadata stripping is only supported for image and audio files.";
            return;
        }

        var saveDir     = Path.GetDirectoryName(FilePath) ?? string.Empty;
        var baseName    = Path.GetFileNameWithoutExtension(FilePath);
        var defaultOut  = Path.Combine(saveDir, $"{baseName}_stripped{ext}");

        var dlg = new SaveFileDialog
        {
            Title      = "Save without metadata",
            FileName   = Path.GetFileName(defaultOut),
            Filter     = $"Original format (*{ext})|*{ext}",
            DefaultExt = ext,
            InitialDirectory = saveDir
        };

        if (dlg.ShowDialog() != true) return;

        var outputPath = dlg.FileName;
        IsLoading     = true;
        StatusMessage = "Stripping metadata…";

        try
        {
            var inputPath = FilePath;
            if (ImageExtensions.Contains(ext))
                await new ImageProcessingService().StripMetadataAsync(inputPath, outputPath);
            else
                await AudioMetadataService.StripAsync(inputPath, outputPath);
            StatusMessage = $"Saved without metadata: {outputPath}";
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
