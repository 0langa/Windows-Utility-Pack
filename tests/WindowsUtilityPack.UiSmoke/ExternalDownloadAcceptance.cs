using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Services.Downloader.Engines;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

internal static class ExternalDownloadAcceptance
{
    internal static async Task<int> RunAsync(string reportPath, string profile, bool galleryOnly = false)
    {
        if (!Path.IsPathFullyQualified(profile)) throw new ArgumentException("Use an absolute isolated profile path.", nameof(profile));
        AppContext.SetData("WindowsUtilityPack.DataDirectory", profile);
        var dependencies = new DependencyManagerService();
        if (!dependencies.Check().AllOk) throw new InvalidOperationException("Install the app-managed tools in the isolated profile first.");
        var output = Path.Combine(Path.GetDirectoryName(reportPath)!, "external-files-" + Guid.NewGuid().ToString("N"));
        var results = new List<object>();
        var failures = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        foreach (var kind in galleryOnly ? new[] { "gallery" } : new[] { "media-audio", "gallery" })
        {
            try
            {
                var target = Path.Combine(output, kind);
                var settings = new DownloaderSettings();
                settings.Media.OutputTemplate = "fixture.%(ext)s";
                settings.Media.EmbedMetadata = true;
                settings.Advanced.CustomEngineArguments = kind == "media-audio"
                    ? "--ignore-config --force-ipv4 --socket-timeout 20 --retries 1"
                    : "--config-ignore --range 1 --retries 1 --http-timeout 20";
                var job = new DownloadJob
                {
                    SourceUrl = kind == "media-audio" ? "https://media.w3.org/2010/05/sintel/trailer.mp4" : "https://commons.wikimedia.org/wiki/File:Example.jpg",
                    Mode = DownloaderMode.MediaDownload
                };
                IDownloadEngine engine = kind == "media-audio" ? new MediaDownloadEngine(dependencies) : new GalleryDownloadEngine(dependencies);
                var plan = new DownloadExecutionPlan
                {
                    TargetDirectory = target,
                    MediaProfile = new DownloadMediaProfile { AudioOnly = true, FormatExpression = "best", PreferredAudioFormat = "mp3" }
                };
                var progress = new CaptureProgress();
                await engine.ExecuteAsync(job, settings, plan, progress, timeout.Token);
                var files = Directory.GetFiles(target, "*", SearchOption.AllDirectories);
                if (kind == "media-audio")
                {
                    var final = progress.FinalPath;
                    if (string.IsNullOrEmpty(final) || !File.Exists(final) || Path.GetExtension(final) != ".mp3")
                        throw new InvalidOperationException("The engine did not report an existing final MP3: " + final);
                    using var audio = TagLib.File.Create(final);
                    if (audio.Properties.Duration.TotalSeconds < 1) throw new InvalidDataException("The exported audio has no duration.");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(progress.FinalPath) || !File.Exists(progress.FinalPath))
                        throw new InvalidDataException("The gallery engine did not report an existing output file for history.");
                    var imagePath = files.FirstOrDefault(p => new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
                        ?? throw new InvalidDataException("The gallery engine produced no image.");
                    using var stream = File.OpenRead(imagePath);
                    var image = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                    if (image.PixelWidth < 1 || image.PixelHeight < 1) throw new InvalidDataException("The gallery image cannot be decoded.");
                    var originalBytes = File.ReadAllBytes(imagePath);
                    var repeatedProgress = new CaptureProgress();
                    await engine.ExecuteAsync(job, settings, plan, repeatedProgress, timeout.Token);
                    if (repeatedProgress.FinalPath != progress.FinalPath || !File.ReadAllBytes(imagePath).SequenceEqual(originalBytes)
                        || repeatedProgress.StatusMessage != "Gallery complete (1 file(s)).")
                        throw new InvalidDataException("An existing gallery file lost its history path, changed, or was counted incorrectly.");
                }
                results.Add(new { Name = kind, Completed = true, Source = job.SourceUrl, Files = files, progress.FinalPath, progress.StatusMessage });
            }
            catch (Exception ex) { failures++; results.Add(new { Name = kind, Completed = false, Error = ex.ToString() }); }
            File.WriteAllText(reportPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        return failures == 0 ? 0 : 1;
    }

    private sealed class CaptureProgress : IProgress<DownloadProgressUpdate>
    {
        public string FinalPath { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;
        public void Report(DownloadProgressUpdate value)
        {
            if (value.Status == DownloadJobStatus.Completed)
            {
                FinalPath = value.OutputFilePath;
                StatusMessage = value.StatusMessage;
            }
        }
    }
}
