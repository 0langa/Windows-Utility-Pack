using System.IO;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services.Storage;
using Xunit;

namespace WindowsUtilityPack.Tests.StorageMaster;

public sealed class CleanupCandidateValidatorTests
{
    [Theory]
    [InlineData("removed")]
    [InlineData("modified")]
    [InlineData("selected")]
    [InlineData("selected-directory")]
    public async Task Validate_DuplicateRequiresIdenticalSurvivorOutsideDeletionSet(string scenario)
    {
        var folder = Path.Combine(Path.GetTempPath(), "wup-survivor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "originals"));
        try
        {
            var originalPath = Path.Combine(folder, "originals", "a.bin");
            var copyPath = Path.Combine(folder, "b.bin");
            File.WriteAllText(originalPath, "identical fixture"); File.Copy(originalPath, copyPath);
            StorageItem Snapshot(string path, DateTime created) => new() { FullPath = path, Name = Path.GetFileName(path), SizeBytes = new FileInfo(path).Length, LastModified = File.GetLastWriteTime(path), CreatedAt = created };
            var original = Snapshot(originalPath, DateTime.MinValue);
            var copy = Snapshot(copyPath, DateTime.MaxValue);
            var root = new StorageItem { FullPath = folder, IsDirectory = true }; root.Children.Add(original); root.Children.Add(copy);
            var group = new DuplicateGroup { Files = [original, copy], GroupKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(originalPath))) };
            var recommendations = await new CleanupRecommendationService().AnalyseAsync(root, [group], default);
            var candidate = Assert.Single(recommendations, r => r.Category == CleanupCategory.DuplicateFiles);
            CleanupCandidateValidator.Validate(candidate);
            if (scenario == "removed") File.Delete(originalPath);
            if (scenario == "modified") { File.WriteAllText(originalPath, "different fixture"); File.SetLastWriteTime(originalPath, original.LastModified); }
            var deletionSet = new List<CleanupRecommendation> { candidate };
            if (scenario == "selected") deletionSet.Add(new() { Item = original, Category = CleanupCategory.TemporaryFiles });
            if (scenario == "selected-directory") deletionSet.Add(new() { Item = new StorageItem { FullPath = Path.GetDirectoryName(originalPath)!, IsDirectory = true }, Category = CleanupCategory.EmptyFolders });
            Assert.Throws<IOException>(() => CleanupCandidateValidator.Validate(candidate, deletionSet));
            Assert.Equal("identical fixture", File.ReadAllText(copyPath));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task DeletionLease_PreventsSurvivorMutationUntilCandidateRemovalCompletes()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wup-lease-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var a = Path.Combine(folder, "a.bin"); var b = Path.Combine(folder, "b.bin");
            File.WriteAllText(a, "owned fixture"); File.Copy(a, b);
            StorageItem Snapshot(string path, DateTime created) => new() { FullPath = path, SizeBytes = new FileInfo(path).Length, LastModified = File.GetLastWriteTime(path), CreatedAt = created };
            var original = Snapshot(a, DateTime.MinValue); var copy = Snapshot(b, DateTime.MaxValue);
            var group = new DuplicateGroup { Files = [original, copy], GroupKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a))) };
            var recommendations = await new CleanupRecommendationService().AnalyseAsync(new StorageItem { FullPath = folder, IsDirectory = true }, [group], default);
            var candidate = Assert.Single(recommendations, r => r.Category == CleanupCategory.DuplicateFiles);
            using (CleanupCandidateValidator.AcquireDeletionLease(candidate, [candidate]))
            {
                Assert.Throws<IOException>(() => File.WriteAllText(a, "cannot replace survivor"));
                File.Delete(b);
                Assert.Equal("owned fixture", File.ReadAllText(a));
            }
            File.WriteAllText(a, "released"); Assert.Equal("released", File.ReadAllText(a));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void Validate_RejectsDirectoryThatBecameNonemptyAfterScan()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wup-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var recommendation = new CleanupRecommendation { Category = CleanupCategory.EmptyFolders,
                Item = new StorageItem { FullPath = folder, IsDirectory = true, IsScanComplete = true } };
            CleanupCandidateValidator.Validate(recommendation);
            File.WriteAllText(Path.Combine(folder, "keep.txt"), "new content");
            Assert.Throws<IOException>(() => CleanupCandidateValidator.Validate(recommendation));
            Assert.True(File.Exists(Path.Combine(folder, "keep.txt")));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void Validate_RejectsFileChangedSinceScan()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "old");
            var info = new FileInfo(path);
            var recommendation = new CleanupRecommendation { Item = new StorageItem
                { FullPath = path, SizeBytes = info.Length, LastModified = info.LastWriteTime } };
            CleanupCandidateValidator.Validate(recommendation);
            File.WriteAllText(path, "new contents");
            Assert.Throws<IOException>(() => CleanupCandidateValidator.Validate(recommendation));
        }
        finally { File.Delete(path); }
    }
}
