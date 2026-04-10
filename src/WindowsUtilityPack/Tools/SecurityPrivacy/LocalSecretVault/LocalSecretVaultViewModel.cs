using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
public class LocalSecretVaultViewModel : ViewModelBase
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

    private ObservableCollection<SecretEntry> _allSecrets = [];
    private SecretEntry?  _selectedSecret;
    private string        _searchText     = string.Empty;
    private string        _editName       = string.Empty;
    private string        _editValue      = string.Empty;
    private string        _editCategory   = string.Empty;
    private string        _editNotes      = string.Empty;
    private bool          _isValueVisible;
    private bool          _isLocked       = true;
    private string        _masterPassword = string.Empty;
    private string        _statusMessage  = string.Empty;

    public ObservableCollection<SecretEntry> Secrets { get; } = [];

    public SecretEntry? SelectedSecret
    {
        get => _selectedSecret;
        set
        {
            if (SetProperty(ref _selectedSecret, value) && value != null)
                LoadEditFields(value);
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

    public AsyncRelayCommand UnlockCommand          { get; }
    public RelayCommand      LockCommand            { get; }
    public RelayCommand      AddSecretCommand       { get; }
    public RelayCommand      SaveSecretCommand      { get; }
    public RelayCommand      DeleteSecretCommand    { get; }
    public RelayCommand      CopyValueCommand       { get; }
    public RelayCommand      ToggleVisibilityCommand { get; }

    public LocalSecretVaultViewModel(IClipboardService clipboard)
    {
        _clipboard = clipboard;

        UnlockCommand          = new AsyncRelayCommand(_ => UnlockAsync());
        LockCommand            = new RelayCommand(_ => Lock(),                  _ => !IsLocked);
        AddSecretCommand       = new RelayCommand(_ => AddSecret(),             _ => !IsLocked);
        SaveSecretCommand      = new RelayCommand(_ => SaveSecret(),            _ => !IsLocked);
        DeleteSecretCommand    = new RelayCommand(_ => DeleteSecret(),          _ => !IsLocked && SelectedSecret != null);
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

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(VaultPath)!);

            if (!File.Exists(VaultPath))
            {
                // Create new vault
                _salt       = RandomNumberGenerator.GetBytes(32);
                _derivedKey = DeriveKey(MasterPassword, _salt);
                _allSecrets.Clear();
                await SaveVaultAsync();
                IsLocked      = false;
                StatusMessage = "New vault created and unlocked.";
                MasterPassword = string.Empty;
                FilterSecrets();
                return;
            }

            // Load and decrypt existing vault
            var json       = await File.ReadAllTextAsync(VaultPath);
            var vaultFile  = JsonSerializer.Deserialize<VaultFile>(json)!;
            _salt          = Convert.FromBase64String(vaultFile.Salt);
            _derivedKey    = DeriveKey(MasterPassword, _salt);

            var plaintext  = Decrypt(Convert.FromBase64String(vaultFile.EncryptedData), _derivedKey);
            var entries    = JsonSerializer.Deserialize<List<SecretEntry>>(plaintext) ?? [];
            _allSecrets    = new ObservableCollection<SecretEntry>(entries);

            IsLocked      = false;
            StatusMessage = $"Vault unlocked. {_allSecrets.Count} secret(s) loaded.";
            MasterPassword = string.Empty;
            FilterSecrets();
        }
        catch (CryptographicException)
        {
            StatusMessage = "Incorrect master password or vault is corrupted.";
            _derivedKey   = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void Lock()
    {
        _derivedKey   = null;
        _salt         = null;
        IsLocked      = true;
        Secrets.Clear();
        _allSecrets.Clear();
        SelectedSecret = null;
        StatusMessage  = "Vault locked.";
    }

    private void AddSecret()
    {
        EditName     = "New Secret";
        EditValue    = string.Empty;
        EditCategory = string.Empty;
        EditNotes    = string.Empty;
        SelectedSecret = null;
        StatusMessage = "Fill in the fields and click Save.";
    }

    private void SaveSecret()
    {
        if (_derivedKey == null) return;

        var encryptedValue = Convert.ToBase64String(
            Encrypt(Encoding.UTF8.GetBytes(EditValue), _derivedKey));

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
        _ = SaveVaultAsync();
        StatusMessage = "Secret saved.";
    }

    private void DeleteSecret()
    {
        if (SelectedSecret == null) return;
        _allSecrets.Remove(SelectedSecret);
        Secrets.Remove(SelectedSecret);
        SelectedSecret = null;
        EditName = EditValue = EditCategory = EditNotes = string.Empty;
        _ = SaveVaultAsync();
        StatusMessage = "Secret deleted.";
    }

    private void CopyValue()
    {
        if (SelectedSecret == null || _derivedKey == null) return;
        try
        {
            var plainBytes = Decrypt(Convert.FromBase64String(SelectedSecret.EncryptedValue), _derivedKey);
            _clipboard.SetText(Encoding.UTF8.GetString(plainBytes));
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
        var encrypted = Encrypt(Encoding.UTF8.GetBytes(json), _derivedKey);

        var vaultFile = new VaultFile
        {
            Salt          = Convert.ToBase64String(_salt),
            EncryptedData = Convert.ToBase64String(encrypted),
        };

        var output = JsonSerializer.Serialize(vaultFile, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(VaultPath, output);
    }

    // --- Crypto helpers ---

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password, salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
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
}
