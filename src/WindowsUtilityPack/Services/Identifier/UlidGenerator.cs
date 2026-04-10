using System.Security.Cryptography;

namespace WindowsUtilityPack.Services.Identifier;

/// <summary>
/// Thread-safe ULID generator.
/// </summary>
public sealed class UlidGenerator : IUlidGenerator
{
    private const string EncodingAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public string Generate()
    {
        Span<byte> timestampBytes = stackalloc byte[6];
        Span<byte> randomnessBytes = stackalloc byte[10];

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        timestampBytes[0] = (byte)(timestamp >> 40);
        timestampBytes[1] = (byte)(timestamp >> 32);
        timestampBytes[2] = (byte)(timestamp >> 24);
        timestampBytes[3] = (byte)(timestamp >> 16);
        timestampBytes[4] = (byte)(timestamp >> 8);
        timestampBytes[5] = (byte)timestamp;

        RandomNumberGenerator.Fill(randomnessBytes);

        Span<byte> data = stackalloc byte[16];
        timestampBytes.CopyTo(data[..6]);
        randomnessBytes.CopyTo(data[6..]);

        Span<char> output = stackalloc char[26];
        EncodeBase32(data, output);
        return output.ToString();
    }

    private static void EncodeBase32(ReadOnlySpan<byte> data, Span<char> output)
    {
        var bitBuffer = 0;
        var bitCount = 0;
        var outputIndex = 0;

        foreach (var value in data)
        {
            bitBuffer = (bitBuffer << 8) | value;
            bitCount += 8;

            while (bitCount >= 5)
            {
                var index = (bitBuffer >> (bitCount - 5)) & 0x1F;
                output[outputIndex++] = EncodingAlphabet[index];
                bitCount -= 5;
            }
        }

        if (bitCount > 0)
        {
            var index = (bitBuffer << (5 - bitCount)) & 0x1F;
            output[outputIndex++] = EncodingAlphabet[index];
        }

        while (outputIndex < output.Length)
        {
            output[outputIndex++] = EncodingAlphabet[0];
        }
    }
}

