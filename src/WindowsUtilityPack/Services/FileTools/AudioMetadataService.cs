using System.IO;

namespace WindowsUtilityPack.Services.FileTools;

public static class AudioMetadataService
{
    public static Task StripAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        outputPath = Path.GetFullPath(outputPath);
        var extension = Path.GetExtension(inputPath);
        if (!extension.Equals(Path.GetExtension(outputPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Keep the original audio extension when removing tags.");
        var temporary = Path.Combine(Path.GetDirectoryName(outputPath)!, ".wup-tags-" + Guid.NewGuid().ToString("N") + extension);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(inputPath, temporary, overwrite: false);
            using (var audio = TagLib.File.Create(temporary))
            {
                audio.RemoveTags(TagLib.TagTypes.AllTags);
                audio.Save();
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, outputPath, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }, cancellationToken);
}
