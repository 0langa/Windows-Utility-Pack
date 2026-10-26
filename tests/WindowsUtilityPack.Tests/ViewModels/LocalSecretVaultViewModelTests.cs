using WindowsUtilityPack.Tools.SecurityPrivacy.LocalSecretVault;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class LocalSecretVaultViewModelTests
{
    [Fact]
    public async Task VaultRoundTrip_LocksOnDeparture_RejectsTampering()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wup-vault-" + Guid.NewGuid().ToString("N"));
        var path = System.IO.Path.Combine(root, "vault.json");
        try
        {
            using var vm = new LocalSecretVaultViewModel(new Clipboard(), path) { MasterPassword = "fixture password" };
            await vm.UnlockAsync();
            vm.EditName = "Unicode 🔐"; vm.EditValue = "pässword\nvalue"; vm.EditNotes = "private note";
            await vm.SaveSecretAsync();
            Assert.Single(vm.Secrets);
            Assert.DoesNotContain("private note", await System.IO.File.ReadAllTextAsync(path));
            Assert.True(System.IO.File.Exists(path + ".bak"));
            vm.Deactivate();
            Assert.True(vm.IsLocked); Assert.Empty(vm.Secrets); Assert.Empty(vm.EditValue); Assert.Empty(vm.EditNotes);
            vm.MasterPassword = "fixture password";
            await vm.UnlockAsync();
            vm.SelectedSecret = Assert.Single(vm.Secrets);
            Assert.Equal("pässword\nvalue", vm.EditValue);
            vm.Deactivate();
            var doc = System.Text.Json.Nodes.JsonNode.Parse(await System.IO.File.ReadAllTextAsync(path))!;
            var data = Convert.FromBase64String(doc["EncryptedData"]!.GetValue<string>());
            data[^1] ^= 1; doc["EncryptedData"] = Convert.ToBase64String(data);
            await System.IO.File.WriteAllTextAsync(path, doc.ToJsonString());
            var damaged = await System.IO.File.ReadAllBytesAsync(path);
            vm.MasterPassword = "fixture password";
            await vm.UnlockAsync();
            Assert.True(vm.IsLocked); Assert.Empty(vm.Secrets);
            Assert.Equal(damaged, await System.IO.File.ReadAllBytesAsync(path));
        }
        finally { if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true); }
    }

    [Fact]
    public void LegacyVault_MigratesWithOriginalEncryptedBackup()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wup-vault-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var path = System.IO.Path.Combine(root, "vault.json");
        try
        {
            var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            var key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2("old password", salt, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
            string LegacyEncrypt(byte[] bytes)
            {
                using var aes = System.Security.Cryptography.Aes.Create();
                aes.Key = key; aes.GenerateIV();
                return Convert.ToBase64String(aes.IV.Concat(aes.EncryptCbc(bytes, aes.IV)).ToArray());
            }
            var entry = new SecretEntry { Name = "old secret", EncryptedValue = LegacyEncrypt(System.Text.Encoding.UTF8.GetBytes("old value")) };
            var json = System.Text.Json.JsonSerializer.Serialize(new { Salt = Convert.ToBase64String(salt), EncryptedData = LegacyEncrypt(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[] { entry })) });
            System.IO.File.WriteAllText(path, json);
            using var store = new WindowsUtilityPack.Services.SecretVaultStore(path);
            var entries = store.Unlock("old password");
            Assert.Equal("old value", store.DecryptValue(Assert.Single(entries).EncryptedValue));
            Assert.Equal(json, System.IO.File.ReadAllText(path + ".bak"));
            var modern = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(path))!;
            Assert.Equal(2, modern["Version"]!.GetValue<int>());
            Assert.Equal(600_000, modern["Iterations"]!.GetValue<int>());
            using var second = new WindowsUtilityPack.Services.SecretVaultStore(path);
            var copy = second.Unlock("old password");
            Assert.Equal("old value", second.DecryptValue(Assert.Single(copy).EncryptedValue));
            store.Save(entries);
            Assert.Throws<System.IO.IOException>(() => second.Save(copy));
        }
        finally { System.IO.Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FailedSave_PreservesEncryptedFileAndCommittedEntries()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wup-vault-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, "vault.json");
        try
        {
            using var vm = new LocalSecretVaultViewModel(new Clipboard(), path) { MasterPassword = "fixture password" };
            await vm.UnlockAsync();
            var original = await System.IO.File.ReadAllBytesAsync(path);
            vm.EditName = "must not commit"; vm.EditValue = "fixture secret";
            using (var locked = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                await vm.SaveSecretAsync();
            Assert.Equal(original, await System.IO.File.ReadAllBytesAsync(path));
            Assert.Empty(vm.Secrets);
            Assert.Contains("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally { System.IO.Directory.Delete(folder, true); }
    }

    private sealed class Clipboard : WindowsUtilityPack.Services.IClipboardService
    {
        public bool TryGetText(out string text) { text = ""; return false; }
        public void SetText(string text) { }
        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
    [Theory]
    [InlineData(1, 250)]
    [InlineData(2, 500)]
    [InlineData(3, 1000)]
    [InlineData(4, 2000)]
    public void UnlockBackoffDelay_GrowsExponentially(int attempts, int expectedMs)
    {
        var delay = LocalSecretVaultViewModel.GetUnlockBackoffDelay(attempts);

        Assert.Equal(expectedMs, (int)delay.TotalMilliseconds);
    }

    [Fact]
    public void UnlockBackoffDelay_IsCapped()
    {
        var delay = LocalSecretVaultViewModel.GetUnlockBackoffDelay(20);

        Assert.Equal(10_000, (int)delay.TotalMilliseconds);
    }

    [Fact]
    public void ClearSensitiveBuffer_ZeroesContents()
    {
        var bytes = new byte[] { 10, 20, 30, 40 };

        LocalSecretVaultViewModel.ClearSensitiveBuffer(bytes);

        Assert.All(bytes, value => Assert.Equal((byte)0, value));
    }
}
