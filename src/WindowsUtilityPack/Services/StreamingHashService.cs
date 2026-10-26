using System.IO;
using System.Security.Cryptography;

namespace WindowsUtilityPack.Services;

internal sealed record HashResults(string Md5, string Sha1, string Sha256, string Sha512, string Crc32);
internal static class StreamingHashService
{
    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(value =>
    {
        var crc = (uint)value;
        for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        return crc;
    }).ToArray();

    public static async Task<HashResults> ComputeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        var buffer = new byte[128 * 1024]; var crc = uint.MaxValue;
        int count;
        while ((count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            md5.AppendData(buffer, 0, count); sha1.AppendData(buffer, 0, count);
            sha256.AppendData(buffer, 0, count); sha512.AppendData(buffer, 0, count);
            for (var i = 0; i < count; i++) crc = (crc >> 8) ^ CrcTable[(crc ^ buffer[i]) & 255];
        }
        return new(Convert.ToHexStringLower(md5.GetHashAndReset()), Convert.ToHexStringLower(sha1.GetHashAndReset()),
            Convert.ToHexStringLower(sha256.GetHashAndReset()), Convert.ToHexStringLower(sha512.GetHashAndReset()), (~crc).ToString("x8"));
    }
}
