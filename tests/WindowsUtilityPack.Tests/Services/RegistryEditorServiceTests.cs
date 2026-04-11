using System.IO;
using Microsoft.Win32;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class RegistryEditorServiceTests
{
    [Fact]
    public async Task SetAndReadAndDeleteValue_RoundTrips()
    {
        var service = new RegistryEditorService();
        var keyPath = CreateTestKeyPath();

        try
        {
            await service.SetValueAsync(keyPath, "Sample", "42", "DWord");

            var values = await service.GetValuesAsync(keyPath);
            Assert.Contains(values, v => v.Name == "Sample" && v.Kind == "DWord");

            await service.DeleteValueAsync(keyPath, "Sample");
            values = await service.GetValuesAsync(keyPath);
            Assert.DoesNotContain(values, v => v.Name == "Sample");
        }
        finally
        {
            DeleteTestKey(keyPath);
        }
    }

    [Fact]
    public async Task BackupAndRestore_RecreatesValues()
    {
        var service = new RegistryEditorService();
        var keyPath = CreateTestKeyPath();
        var backupFile = Path.Combine(Path.GetTempPath(), $"wup-reg-{Guid.NewGuid():N}.json");

        try
        {
            await service.SetValueAsync(keyPath, "Name", "Alpha", "String");
            await service.BackupAsync(keyPath, backupFile);

            await service.DeleteValueAsync(keyPath, "Name");
            await service.RestoreAsync(backupFile);

            var values = await service.GetValuesAsync(keyPath);
            Assert.Contains(values, v => v.Name == "Name" && v.DisplayData == "Alpha");
        }
        finally
        {
            DeleteTestKey(keyPath);
            TryDelete(backupFile);
        }
    }

    private static string CreateTestKeyPath()
    {
        var keyPath = $"Software\\WindowsUtilityPackTests\\{Guid.NewGuid():N}";
        using var _ = Registry.CurrentUser.CreateSubKey(keyPath);
        return keyPath;
    }

    private static void DeleteTestKey(string keyPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
        catch { }
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch { }
    }
}