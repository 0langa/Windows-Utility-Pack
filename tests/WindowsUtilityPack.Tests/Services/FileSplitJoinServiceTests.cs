using System.Text;
using System.IO;
using WindowsUtilityPack.Services.FileTools;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class FileSplitJoinServiceTests
{
    [Theory]
    [InlineData("cancel")]
    [InlineData("checksum")]
    [InlineData("missing-manifest")]
    [InlineData("invalid-manifest")]
    [InlineData("missing-part")]
    [InlineData("duplicate-part")]
    public async Task Join_FailurePreservesExistingDestination(string failure)
    {
        using var fixture = new Fixture();
        var service = new FileSplitJoinService();
        var source = Path.Combine(fixture.Root, "source.bin");
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4, 5, 6]);
        var split = await service.SplitAsync(source, fixture.Root, 2, CancellationToken.None);
        var output = Path.Combine(fixture.Root, "joined.bin");
        byte[] original = [99, 98, 97];
        await File.WriteAllBytesAsync(output, original);
        switch (failure)
        {
            case "checksum": await File.WriteAllBytesAsync(split.PartFiles[0], [7, 8]); break;
            case "missing-manifest": File.Delete(split.ManifestPath); break;
            case "invalid-manifest": await File.WriteAllTextAsync(split.ManifestPath, "{ broken"); break;
            case "missing-part": File.Delete(split.PartFiles[1]); break;
            case "duplicate-part": File.Copy(split.PartFiles[0], source + ".part1"); break;
        }

        await Assert.ThrowsAnyAsync<Exception>(() => service.JoinAsync(split.PartFiles[0], output, true, new CancellationToken(failure == "cancel")));

        Assert.Equal(original, await File.ReadAllBytesAsync(output));
        Assert.Empty(Directory.GetFiles(fixture.Root, ".wup-join-*.tmp"));
    }

    [Fact]
    public async Task Join_CancellationDuringCopyPreservesDestination()
    {
        using var fixture = new Fixture();
        var service = new FileSplitJoinService();
        var source = Path.Combine(fixture.Root, "source.bin");
        await File.WriteAllBytesAsync(source, new byte[512 * 1024]);
        var split = await service.SplitAsync(source, fixture.Root, 256 * 1024, CancellationToken.None);
        var output = Path.Combine(fixture.Root, "joined.bin");
        await File.WriteAllTextAsync(output, "original");
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.JoinAsync(split.PartFiles[0], output, true, cancellation.Token,
            new InlineProgress(_ => cancellation.Cancel())));
        Assert.Equal("original", await File.ReadAllTextAsync(output));
        Assert.Empty(Directory.GetFiles(fixture.Root, ".wup-join-*.tmp"));
    }

    [Fact]
    public async Task Join_RejectsInputAsOutput()
    {
        using var fixture = new Fixture();
        var service = new FileSplitJoinService();
        var source = Path.Combine(fixture.Root, "source.bin");
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);
        var split = await service.SplitAsync(source, fixture.Root, 2, CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => service.JoinAsync(split.PartFiles[0], split.PartFiles[0], false, CancellationToken.None));
        Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(split.PartFiles[0]));
    }

    [Fact]
    public async Task Join_ChangedPartLengthWithoutChecksum_DoesNotPublishOutput()
    {
        using var fixture = new Fixture();
        var service = new FileSplitJoinService();
        var source = Path.Combine(fixture.Root, "source.bin");
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);
        var split = await service.SplitAsync(source, fixture.Root, 2, CancellationToken.None);
        var output = Path.Combine(fixture.Root, "joined.bin");
        var changed = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => service.JoinAsync(split.PartFiles[0], output, false, CancellationToken.None,
            new InlineProgress(_ =>
            {
                if (changed) return;
                changed = true;
                File.WriteAllBytes(split.PartFiles[1], [3, 4, 5]);
            })));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task Split_RefusesToOverwriteExistingPartSet()
    {
        using var fixture = new Fixture();
        var service = new FileSplitJoinService();
        var source = Path.Combine(fixture.Root, "source.bin");
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);
        var split = await service.SplitAsync(source, fixture.Root, 2, CancellationToken.None);
        await File.WriteAllBytesAsync(source, [9, 9, 9, 9]);
        await Assert.ThrowsAsync<IOException>(() => service.SplitAsync(source, fixture.Root, 2, CancellationToken.None));
        Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(split.PartFiles[0]));
    }

    [Fact]
    public async Task Split_CancellationRemovesOnlyPartsCreatedByOperation()
    {
        using var fixture = new Fixture();
        var service = new FileSplitJoinService();
        var source = Path.Combine(fixture.Root, "source.bin");
        await File.WriteAllBytesAsync(source, new byte[512 * 1024]);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SplitAsync(source, fixture.Root, 256 * 1024, cancellation.Token,
            new InlineProgress(_ => cancellation.Cancel())));
        Assert.Equal([source], Directory.GetFiles(fixture.Root));
    }

    [Fact]
    public async Task SplitAndJoin_EmptyFileRoundTrips()
    {
        using var fixture = new Fixture();
        var service = new FileSplitJoinService();
        var source = Path.Combine(fixture.Root, "empty.bin");
        await File.WriteAllBytesAsync(source, []);
        var split = await service.SplitAsync(source, fixture.Root, 1024, CancellationToken.None);
        var output = Path.Combine(fixture.Root, "joined.bin");
        var joined = await service.JoinAsync(Assert.Single(split.PartFiles), output, true, CancellationToken.None);
        Assert.True(joined.ChecksumVerified);
        Assert.Equal(0, new FileInfo(output).Length);
    }

    private sealed class InlineProgress(Action<FileSplitJoinProgress> report) : IProgress<FileSplitJoinProgress>
    {
        public void Report(FileSplitJoinProgress value) => report(value);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "wup-file-integrity-" + Guid.NewGuid().ToString("N"));
        public Fixture() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

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
