using System.IO;
using System.Net.Http;
using System.Text;

namespace WindowsUtilityPack.Services;

public static class BoundedHttpContent
{
    public const int DefaultMaximumBytes = 4 * 1024 * 1024;

    public static async Task<string> ReadTextAsync(HttpContent content, CancellationToken cancellationToken, int maximumBytes = DefaultMaximumBytes)
    {
        if (content.Headers.ContentLength > maximumBytes) throw new InvalidDataException($"Response exceeds the {maximumBytes / 1024} KiB limit.");
        await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var bytes = new MemoryStream();
        var buffer = new byte[16384];
        while (true)
        {
            var count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (bytes.Length + count > maximumBytes) throw new InvalidDataException($"Response exceeds the {maximumBytes / 1024} KiB limit.");
            bytes.Write(buffer, 0, count);
        }
        var encoding = Encoding.UTF8;
        var charset = content.Headers.ContentType?.CharSet?.Trim('"');
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try { encoding = Encoding.GetEncoding(charset); }
            catch (ArgumentException) { throw new InvalidDataException($"Unsupported response character set: {charset}"); }
        }
        bytes.Position = 0;
        using var reader = new StreamReader(bytes, encoding, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
