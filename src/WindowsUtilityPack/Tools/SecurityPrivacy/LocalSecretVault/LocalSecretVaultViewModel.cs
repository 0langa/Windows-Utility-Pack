using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SecurityPrivacy.LocalSecretVault;

/// <summary>Represents a single encrypted secret entry.</summary>
public class SecretEntry
{
    public string   Id             { get; set; } = Guid.NewGuid().ToString();
    public string   Name           { get; set; } = string.Empty;
    public string   EncryptedValue { get; set; } = string.Empty;
    public string   Category       { get; set; } = string.Empty;
    public string   Notes          { get; set; } = string.Empty;
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt      { get; set; } = DateTime.UtcNow;
}

/// <summary>Vault file format stored on disk.</summary>
internal class VaultFile
{
    public string Salt          { get; set; } = string.Empty;
    public string EncryptedData { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the Local Secret Vault tool.
/// All secrets are AES-256 encrypted with a master password derived key (PBKDF2 / SHA-256).
/// </summary>
public class LocalSecretVaultViewModel : ViewModelBase, IDisposable
{
    private static readonly string VaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowsUtilityPack", "vault.json");

    private const int Iterations = 100_000;
    private const int KeySize    = 32; // 256-bit AES key
    private const int IvSize     = 16;

    private readonly IClipboardService _clipboard;

    private byte[]? _derivedKey;
    private byte[]? _salt;

    private DispatcherTimer? _autoLockTimer;
    private DateTime         _lastActivityUtc;

    private ObservableCollection<SecretEntry> _allSecrets = [];
    private SecretEntry?  _selectedSecret;
    private string        _searchText       = string.Empty;
    private string        _editName         = string.Empty;
    private string        _editValue        = string.Empty;
    private string        _editCategory     = string.Empty;
    private string        _editNotes        = string.Empty;
    private bool          _isValueVisible;
    private bool          _isLocked         = true;
    private string        _masterPassword   = string.Empty;
    private string        _statusMessage    = string.Empty;
    private int           _autoLockMinutes  = 5;
    private int           _failedUnlockAttempts;
    private DateTimeOffset? _lockedOutUntilUtc;

    public ObservableCollection<SecretEntry> Secrets { get; } = [];

    public SecretEntry? SelectedSecret
    {
        get => _selectedSecret;
        set
        {
            if (SetProperty(ref _selectedSecret, value) && value != null)
            {
                ResetActivityTimer();
                LoadEditFields(value);
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                FilterSecrets();
        }
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public string EditValue
    {
        get => _editValue;
        set => SetProperty(ref _editValue, value);
    }

    public string EditCategory
    {
        get => _editCategory;
        set => SetProperty(ref _editCategory, value);
    }

    public string EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    public bool IsValueVisible
    {
        get => _isValueVisible;
        set => SetProperty(ref _isValueVisible, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        set => SetProperty(ref _isLocked, value);
    }

    public string MasterPassword
    {
        get => _masterPassword;
        set => SetProperty(ref _masterPassword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Minutes of inactivity before the vault auto-locks. Set to 0 to disable.
    /// </summary>
    public int AutoLockMinutes
    {
        get => _autoLockMinutes;
        set => SetProperty(ref _autoLockMinutes, Math.Max(0, value));
    }

    public AsyncRelayCommand UnlockCommand          { get; }
    public RelayCommand      LockCommand            { get; }
    public RelayCommand      AddSecretCommand       { get; }
    public AsyncRelayCommand  SaveSecretCommand      { get; }
    public AsyncRelayCommand  DeleteSecretCommand    { get; }
    public RelayCommand      CopyValueCommand       { get; }
    public RelayCommand      ToggleVisibilityCommand { get; }

    public LocalSecretVaultViewModel(IClipboardService clipboard)
    {
        _clipboard = clipboard;

        UnlockCommand          = new AsyncRelayCommand(_ => UnlockAsync());
        LockCommand            = new RelayCommand(_ => Lock(),                  _ => !IsLocked);
        AddSecretCommand       = new RelayCommand(_ => AddSecret(),             _ => !IsLocked);
        SaveSecretCommand      = new AsyncRelayCommand(_ => SaveSecretAsync(),  _ => !IsLocked);
        DeleteSecretCommand    = new AsyncRelayCommand(_ => DeleteSecretAsync(), _ => !IsLocked && SelectedSecret != null);
        CopyValueCommand       = new RelayCommand(_ => CopyValue(),             _ => !IsLocked && SelectedSecret != null);
        ToggleVisibilityCommand = new RelayCommand(_ => IsValueVisible = !IsValueVisible);
    }

    private async Task UnlockAsync()
    {
        if (string.IsNullOrWhiteSpace(MasterPassword))
        {
            StatusMessage = "Please enter a master password.";
            return;
        }

        if (_lockedOutUntilUtc.HasValue && DateTimeOffset.UtcNow < _lockedOutUntilUtc.Value)
        {
            var remaining = _lockedOutUntilUtc.Value - DateTimeOffset.UtcNow;
            StatusMessage = $"Too many failed attempts. Try again in {Math.Ceiling(remaining.TotalSeconds):F0}s.";
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(VaultPath)!);

            if (!File.Exists(VaultPath))
            {
                // Create new vault
                ReplaceSalt(RandomNumberGenerator.GetBytes(32));
                ReplaceDerivedKey(DeriveKey(MasterPassword, _salt!));
                _allSecrets.Clear();
                await SaveVaultAsync();
                IsLocked      = false;
                StatusMessage = "New vault created and unlocked.";
                MasterPassword = string.Empty;
                _failedUnlockAttempts = 0;
                _lockedOutUntilUtc = null;
                StartAutoLockTimer();
                FilterSecrets();
                return;
            }

            // Load and decrypt existing vault
            var json       = await File.ReadAllTextAsync(VaultPath);
            var vaultFile  = JsonSerializer.Deserialize<VaultFile>(json)!;
            ReplaceSalt(Convert.FromBase64String(vaultFile.Salt));
            ReplaceDerivedKey(DeriveKey(MasterPassword, _salt!));

            var plaintextBytes = Decrypt(Convert.FromBase64String(vaultFile.EncryptedData), _derivedKey!);
            var plaintextJson = Encoding.UTF8.GetString(plaintextBytes);
            ClearSensitiveBuffer(plaintextBytes);
            var entries = JsonSerializer.Deserialize<List<SecretEntry>>(plaintextJson) ?? [];
            _allSecrets    = new ObservableCollection<SecretEntry>(entries);

            IsLocked      = false;
            StatusMessage = $"Vault unlocked. {_allSecrets.Count} secret(s) loaded.";
            MasterPassword = string.Empty;
            _failedUnlockAttempts = 0;
            _lockedOutUntilUtc = null;
            StartAutoLockTimer();
            FilterSecrets();
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            _failedUnlockAttempts++;
            ReplaceDerivedKey(null);
            var delay = GetUnlockBackoffDelay(_failedUnlockAttempts);

            if (_failedUnlockAttempts >= 10)
            {
                _lockedOutUntilUtc = DateTimeOffset.UtcNow.AddMinutes(2);
                StatusMessage = "Too many failed attempts. Vault unlock is temporarily locked for 2 minutes.";
            }
            else
            {
                StatusMessage = $"Incorrect master password or vault is corrupted. Retry in {delay.TotalSeconds:F1}s.";
            }

            await Task.Delay(delay);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            MasterPassword = string.Empty;
        }
    }

    private void Lock()
    {
        StopAutoLockTimer();
        ReplaceDerivedKey(null);
        ReplaceSalt(null);
        IsLocked      = true;
        Secrets.Clear();
        _allSecrets.Clear();
        SelectedSecret = null;
        EditValue = string.Empty;
        StatusMessage  = "Vault locked.";
    }

    private void AddSecret()
    {
        ResetActivityTimer();
        EditName     = "New Secret";
        EditValue    = string.Empty;
        EditCategory = string.Empty;
        EditNotes    = string.Empty;
        SelectedSecret = null;
        StatusMessage = "Fill in the fields and click Save.";
    }

    private async Task SaveSecretAsync()
    {
        if (_derivedKey == null) return;
        ResetActivityTimer();
        var plaintextBytes = Encoding.UTF8.GetBytes(EditValue);
        var encryptedBytes = Encrypt(plaintextBytes, _derivedKey);
        ClearSensitiveBuffer(plaintextBytes);
        var encryptedValue = Convert.ToBase64String(encryptedBytes);
        ClearSensitiveBuffer(encryptedBytes);

        if (SelectedSecret == null)
        {
            // New secret
            var entry = new SecretEntry
            {
                Name           = EditName,
                EncryptedValue = encryptedValue,
                Category       = EditCategory,
                Notes          = EditNotes,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
            };
            _allSecrets.Add(entry);
            SelectedSecret = entry;
        }
        else
        {
            SelectedSecret.Name           = EditName;
            SelectedSecret.EncryptedValue = encryptedValue;
            SelectedSecret.Category       = EditCategory;
            SelectedSecret.Notes          = EditNotes;
            SelectedSecret.UpdatedAt      = DateTime.UtcNow;
        }

        FilterSecrets();
        try
        {
            await SaveVaultAsync();
            StatusMessage = "Secret saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Secret saved in memory but vault write failed: {ex.Message}";
        }
    }

    private async Task DeleteSecretAsync()
    {
        if (SelectedSecret == null) return;
        _allSecrets.Remove(SelectedSecret);
        Secrets.Remove(SelectedSecret);
        SelectedSecret = null;
        EditName = EditValue = EditCategory = EditNotes = string.Empty;
        try
        {
            await SaveVaultAsync();
            StatusMessage = "Secret deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Secret removed but vault write failed: {ex.Message}";
        }
    }

    private void CopyValue()
    {
        if (SelectedSecret == null || _derivedKey == null) return;
        ResetActivityTimer();
        try
        {
            var plainBytes = Decrypt(Convert.FromBase64String(SelectedSecret.EncryptedValue), _derivedKey);
            _clipboard.SetText(Encoding.UTF8.GetString(plainBytes));
            ClearSensitiveBuffer(plainBytes);
            StatusMessage = "Value copied to clipboard.";
        }
        catch { StatusMessage = "Failed to decrypt value."; }
    }

    private void LoadEditFields(SecretEntry entry)
    {
        EditName     = entry.Name;
        EditCategory = entry.Category;
        EditNotes    = entry.Notes;

        // Decrypt value for edit panel
        if (_derivedKey != null && !string.IsNullOrEmpty(entry.EncryptedValue))
        {
            try
            {
                var plainBytes = Decrypt(Convert.FromBase64String(entry.EncryptedValue), _derivedKey);
                EditValue = Encoding.UTF8.GetString(plainBytes);
                ClearSensitiveBuffer(plainBytes);
            }
            catch { EditValue = "<decryption error>"; }
        }
        else
        {
            EditValue = string.Empty;
        }
    }

    private void FilterSecrets()
    {
        Secrets.Clear();
        var filter = SearchText?.Trim() ?? string.Empty;
        foreach (var s in _allSecrets)
        {
            if (string.IsNullOrEmpty(filter)
                || s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || s.Category.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                Secrets.Add(s);
            }
        }
    }

    private async Task SaveVaultAsync()
    {
        if (_derivedKey == null || _salt == null) return;

        var json      = JsonSerializer.Serialize(_allSecrets.ToList());
        var plaintextBytes = Encoding.UTF8.GetBytes(json);
        var encrypted = Encrypt(plaintextBytes, _derivedKey);
        ClearSensitiveBuffer(plaintextBytes);

        var vaultFile = new VaultFile
        {
            Salt          = Convert.ToBase64String(_salt),
            EncryptedData = Convert.ToBase64String(encrypted),
        };
        ClearSensitiveBuffer(encrypted);

        var output = JsonSerializer.Serialize(vaultFile, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(VaultPath, output);
    }

    private void StartAutoLockTimer()
    {
        _lastActivityUtc = DateTime.UtcNow;
        if (_autoLockTimer == null)
        {
            _autoLockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autoLockTimer.Tick += OnAutoLockTimerTick;
        }
        _autoLockTimer.Start();
    }

    private void StopAutoLockTimer()
    {
        _autoLockTimer?.Stop();
    }

    private void OnAutoLockTimerTick(object? sender, EventArgs e)
    {
        if (AutoLockMinutes <= 0 || IsLocked) return;
        if ((DateTime.UtcNow - _lastActivityUtc).TotalMinutes >= AutoLockMinutes)
        {
            StatusMessage = $"Vault auto-locked after {AutoLockMinutes} minute(s) of inactivity.";
            Lock();
        }
    }

    private void ResetActivityTimer()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    internal static TimeSpan GetUnlockBackoffDelay(int failedAttempts)
    {
        var sanitizedAttempts = Math.Max(1, failedAttempts);
        var delayMs = Math.Min(10_000, 250 * (int)Math.Pow(2, Math.Min(sanitizedAttempts - 1, 6)));
        return TimeSpan.FromMilliseconds(delayMs);
    }

    private void ReplaceDerivedKey(byte[]? newKey)
    {
        ClearSensitiveBuffer(_derivedKey);
        _derivedKey = newKey;
    }

    private void ReplaceSalt(byte[]? newSalt)
    {
        ClearSensitiveBuffer(_salt);
        _salt = newSalt;
    }

    internal static void ClearSensitiveBuffer(byte[]? buffer)
    {
        if (buffer is null)
        {
            return;
        }

        Array.Clear(buffer, 0, buffer.Length);
    }

    // --- Crypto helpers ---

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
    }

    private static byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var enc    = aes.CreateEncryptor();
        var ciphertext   = enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
        // Prepend IV to ciphertext
        var result = new byte[IvSize + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, IvSize);
        Buffer.BlockCopy(ciphertext, 0, result, IvSize, ciphertext.Length);
        return result;
    }

    private static byte[] Decrypt(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        var iv         = new byte[IvSize];
        var ciphertext = new byte[data.Length - IvSize];
        Buffer.BlockCopy(data, 0, iv, 0, IvSize);
        Buffer.BlockCopy(data, IvSize, ciphertext, 0, ciphertext.Length);
        aes.IV = iv;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }

    public void Dispose()
    {
        if (_autoLockTimer != null)
        {
            _autoLockTimer.Stop();
            _autoLockTimer.Tick -= OnAutoLockTimerTick;
            _autoLockTimer = null;
        }
        Lock();
    }
}
