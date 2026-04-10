using System.Text;
using System.IO;
using WindowsUtilityPack.Services.FileTools;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class FileSplitJoinServiceTests
{
    [Fact]
    public async Task SplitAndJoin_RoundTripsAndVerifiesChecksum()
    {
        var service = new FileSplitJoinService();
        var root = Path.Combine(Path.GetTempPath(), "wup-split-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var sourcePath = Path.Combine(root, "sample.txt");
            var payload = string.Join(Environment.NewLine, Enumerable.Range(0, 2500).Select(i => $"line-{i:D4}"));
            await File.WriteAllTextAsync(sourcePath, payload, Encoding.UTF8);

            var splitDir = Path.Combine(root, "parts");
            var split = await service.SplitAsync(sourcePath, splitDir, 1024, CancellationToken.None);

            Assert.True(split.PartFiles.Count > 1);
            Assert.True(File.Exists(split.ManifestPath));

            var joinedPath = Path.Combine(root, "joined.txt");
            var join = await service.JoinAsync(split.PartFiles[0], joinedPath, verifyChecksum: true, CancellationToken.None);

            Assert.True(join.ChecksumVerified);
            var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
            var joinedBytes = await File.ReadAllBytesAsync(joinedPath);
            Assert.Equal(sourceBytes, joinedBytes);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task SplitAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        var service = new FileSplitJoinService();
        var root = Path.Combine(Path.GetTempPath(), "wup-split-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var sourcePath = Path.Combine(root, "sample.bin");
            await File.WriteAllBytesAsync(sourcePath, new byte[64 * 1024]);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                service.SplitAsync(sourcePath, root, 1024, cts.Token));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
