using System.IO;
using System.Text;

namespace WindowsUtilityPack.Services;

internal static class BoundedTextFile
{
    public static async IAsyncEnumerable<string> ReadLinesAsync(StreamReader reader, int maximumLineCharacters,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new char[8192]; var line = new StringBuilder(); var previousCr = false; long total = 0;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += count;
            if (total > 256L * 1024 * 1024) throw new InvalidDataException("Log analysis is limited to 256 MiB of text.");
            for (var i = 0; i < count; i++)
            {
                var ch = buffer[i];
                if (previousCr && ch == '\n') { previousCr = false; continue; }
                previousCr = ch == '\r';
                if (ch is '\r' or '\n') { cancellationToken.ThrowIfCancellationRequested(); yield return line.ToString(); line.Clear(); }
                else
                {
                    if (line.Length == maximumLineCharacters) throw new InvalidDataException($"A log line exceeds {maximumLineCharacters:N0} characters.");
                    line.Append(ch);
                }
            }
        }
        if (line.Length > 0) yield return line.ToString();
    }
    public const int MaximumEditorCharacters = 1_000_000;
    public static async Task<string> ReadAsync(string path, int maxCharacters = MaximumEditorCharacters, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > (long)maxCharacters * 4 + 4) throw new InvalidDataException($"The text exceeds the {maxCharacters:N0} character limit.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        var text = new StringBuilder(); var buffer = new char[8192];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if ((long)text.Length + count > maxCharacters) throw new InvalidDataException($"The text exceeds the {maxCharacters:N0} character limit.");
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }
}
