using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using WindowsUtilityPack.Tools.SecurityPrivacy.LocalSecretVault;

namespace WindowsUtilityPack.Services;

/// <summary>A versioned, authenticated vault. Only encrypted bytes are written to disk.</summary>
internal sealed class SecretVaultStore(string path) : IDisposable
{
    private const int CurrentIterations = 600_000;
    private const int MaximumBytes = 16 * 1024 * 1024;
    private byte[]? _key;
    private byte[]? _salt;
    private byte[]? _diskHash;
    private int _iterations;

    public List<SecretEntry> Unlock(string password)
    {
        Dispose();
        using var gate = AcquireWriteLock();
        try
        {
            List<SecretEntry> entries;
            if (!File.Exists(path))
            {
                _salt = RandomNumberGenerator.GetBytes(32);
                _iterations = CurrentIterations;
                _key = Derive(password, _salt, _iterations);
                entries = [];
                Write(entries);
                return entries;
            }
            var bytes = ReadBounded();
            _diskHash = SHA256.HashData(bytes);
            var file = JsonSerializer.Deserialize<VaultFile>(bytes) ?? throw new InvalidDataException("The vault file is empty.");
            if (file.Version is not (0 or 1 or 2)) throw new InvalidDataException("Unsupported vault version.");
            _salt = Convert.FromBase64String(file.Salt);
            if (_salt.Length != 32) throw new InvalidDataException("Invalid vault salt.");
            _iterations = file.Version < 2 ? 100_000 : file.Iterations;
            if (_iterations < 100_000 || _iterations > 2_000_000) throw new InvalidDataException("Invalid vault key parameters.");
            _key = Derive(password, _salt, _iterations);
            var plain = file.Version < 2
                ? DecryptLegacy(Convert.FromBase64String(file.EncryptedData), _key)
                : Decrypt(Convert.FromBase64String(file.EncryptedData));
            try { entries = JsonSerializer.Deserialize<List<SecretEntry>>(plain) ?? throw new InvalidDataException("Invalid vault contents."); }
            finally { CryptographicOperations.ZeroMemory(plain); }
            if (entries.Count > 10_000 || entries.Any(e => e is null || e.Name is null || e.Category is null || e.Notes is null || e.EncryptedValue is null))
                throw new InvalidDataException("Invalid vault entries.");
            if (file.Version < 2)
            {
                // Convert every entry before replacing the old file. The old encrypted file is retained as .bak.
                var newSalt = RandomNumberGenerator.GetBytes(32);
                var newKey = Derive(password, newSalt, CurrentIterations);
                try
                {
                    foreach (var entry in entries)
                    {
                        var value = DecryptLegacy(Convert.FromBase64String(entry.EncryptedValue), _key);
                        try { entry.EncryptedValue = Convert.ToBase64String(Encrypt(value, newKey)); }
                        finally { CryptographicOperations.ZeroMemory(value); }
                    }
                    CryptographicOperations.ZeroMemory(_key);
                    _key = newKey; newKey = null!;
                    _salt = newSalt;
                    _iterations = CurrentIterations;
                    Write(entries);
                }
                finally { if (newKey is not null) CryptographicOperations.ZeroMemory(newKey); }
            }
            return entries;
        }
        catch { Dispose(); throw; }
    }

    public void Save(IReadOnlyCollection<SecretEntry> entries)
    {
        if (_key is null) throw new InvalidOperationException("The vault is locked.");
        using var gate = AcquireWriteLock();
        var currentHash = File.Exists(path) ? SHA256.HashData(ReadBounded()) : null;
        if (_diskHash is null || currentHash is null || !CryptographicOperations.FixedTimeEquals(_diskHash, currentHash))
            throw new IOException("The vault changed on disk. Lock and unlock it before saving again.");
        Write(entries);
    }

    public string EncryptValue(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        try { return Convert.ToBase64String(Encrypt(bytes, _key ?? throw new InvalidOperationException("The vault is locked."))); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public string DecryptValue(string value)
    {
        var bytes = Decrypt(Convert.FromBase64String(value));
        try { return System.Text.Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private void Write(IReadOnlyCollection<SecretEntry> entries)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(entries);
        byte[] encrypted;
        try
        {
            if (plain.Length > MaximumBytes / 2) throw new InvalidDataException("The vault exceeds the 8 MiB content limit.");
            encrypted = Encrypt(plain, _key!);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
        var json = JsonSerializer.Serialize(new VaultFile
        {
            Version = 2, Iterations = _iterations,
            Salt = Convert.ToBase64String(_salt!), EncryptedData = Convert.ToBase64String(encrypted)
        });
        AtomicFile.WriteAllText(path, json, backupPath: path + ".bak");
        _diskHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
    }

    private FileStream AcquireWriteLock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path + ".write.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite,
            FileShare.None, 1, FileOptions.DeleteOnClose);
    }

    private byte[] ReadBounded()
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("The vault exceeds the 16 MiB file limit.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

    // nonce (12) + authentication tag (16) + ciphertext. The format version selects GCM, never a heuristic.
    private static byte[] Encrypt(byte[] plain, byte[] key)
    {
        var result = new byte[28 + plain.Length];
        RandomNumberGenerator.Fill(result.AsSpan(0, 12));
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(result.AsSpan(0, 12), plain, result.AsSpan(28), result.AsSpan(12, 16));
        return result;
    }

    private byte[] Decrypt(byte[] data)
    {
        if (data.Length < 28) throw new CryptographicException("Invalid encrypted value.");
        var plain = new byte[data.Length - 28];
        using var aes = new AesGcm(_key ?? throw new InvalidOperationException("The vault is locked."), 16);
        aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain);
        return plain;
    }

    private static byte[] DecryptLegacy(byte[] data, byte[] key)
    {
        if (data.Length < 32 || data.Length % 16 != 0) throw new CryptographicException("Invalid legacy encrypted value.");
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.DecryptCbc(data.AsSpan(16), data.AsSpan(0, 16));
    }

    public void Dispose()
    {
        if (_key is not null) CryptographicOperations.ZeroMemory(_key);
        _key = null; _salt = null; _diskHash = null;
    }
}
