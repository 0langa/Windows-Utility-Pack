using System.IO;
using System.Text;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.DeveloperProductivity.DiffTool;
using WindowsUtilityPack.Tools.DeveloperProductivity.RegexTester;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class BoundedWorkTests
{
    [Fact]
    public void Diff_RejectsExcessiveMatrix_AndHonorsPreCancellation()
    {
        Assert.Throws<InvalidDataException>(() => DiffToolViewModel.ComputeDiff(Enumerable.Repeat("left", 2500).ToArray(), Enumerable.Repeat("right", 2500).ToArray(), false, false));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => DiffToolViewModel.ComputeDiff(["left"], ["right"], false, false, cancelled.Token));
        var lines = DiffToolViewModel.ComputeDiff(["Grüße", "old"], ["Grüße", "new"], false, false);
        Assert.Single(lines, l => l.Type == DiffType.Unchanged);
        Assert.Contains(lines, l => l.Type == DiffType.Removed && l.Content == "old");
        Assert.Contains(lines, l => l.Type == DiffType.Added && l.Content == "new");
    }

    [Fact]
    public void Regex_BoundsResultsAndPreviews()
    {
        using var vm = new RegexTesterViewModel { InputText = new string('a', 10_000), Pattern = "a" };
        vm.RunRegex();
        Assert.Equal(1000, vm.Matches.Count); Assert.Contains("first", vm.StatusMessage);
        vm.Pattern = ".+"; vm.RunRegex();
        Assert.Equal(10_000, Assert.Single(vm.Matches).Length);
        Assert.True(vm.Matches[0].Value.Length <= 513);
    }

    [Fact]
    public async Task StreamingHashes_MatchKnownVectorAcrossShortReads_AndCancel()
    {
        await using var stream = new ShortReadStream(Encoding.ASCII.GetBytes("abc"));
        var hashes = await StreamingHashService.ComputeAsync(stream);
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", hashes.Md5);
        Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", hashes.Sha1);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hashes.Sha256);
        Assert.Equal("352441c2", hashes.Crc32);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); stream.Position = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StreamingHashService.ComputeAsync(stream, cancelled.Token));
    }

    [Fact]
    public async Task LogAnalysis_CountsAllMatches_ButBoundsRetainedRowsAndLineLength()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(path, Enumerable.Repeat("INFO Grüß dich", 10_001));
            var result = await new LogFileAnalyzerService().AnalyzeAsync(path, null, null);
            Assert.Equal(10_001, result.TotalLines); Assert.Equal(10_001, result.MatchedLines); Assert.Equal(5000, result.Entries.Count);
            await File.WriteAllTextAsync(path, new string('x', 65_537));
            await Assert.ThrowsAsync<InvalidDataException>(() => new LogFileAnalyzerService().AnalyzeAsync(path, null, null));
        }
        finally { File.Delete(path); }
    }

    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
