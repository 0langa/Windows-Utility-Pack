using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SecurityPrivacy.LocalSecretVault;

public class SecretEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string EncryptedValue { get; set; } = "";
    public string Category { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

internal class VaultFile
{
    public int Version { get; set; }
    public int Iterations { get; set; }
    public string Salt { get; set; } = "";
    public string EncryptedData { get; set; } = "";
}

public class LocalSecretVaultViewModel : ViewModelBase, IDisposable, INavigationGuard, IViewLifetime
{
    private readonly string _vaultPath;
    private readonly bool _allowLegacyMigration;
    private readonly IClipboardService _clipboard;
    private readonly IUserDialogService _dialogs;
    private SecretVaultStore? _store;
    private List<SecretEntry> _allSecrets = [];
    private SecretEntry? _selectedSecret;
    private string _searchText = "", _editName = "", _editValue = "", _editCategory = "", _editNotes = "";
    private string _masterPassword = "", _statusMessage = "", _draftBaseline = "";
    private bool _isValueVisible, _isBusy, _disposed, _isLocked = true, _updatingSelection;
    private int _failedUnlockAttempts;
    private DateTimeOffset? _lockedOutUntilUtc;

    public ObservableCollection<SecretEntry> Secrets { get; } = [];
    public SecretEntry? SelectedSecret
    {
        get => _selectedSecret;
        set
        {
            if (_updatingSelection || _selectedSecret == value) return;
            if (IsBusy || !ConfirmDiscard()) { OnPropertyChanged(); return; }
            SetSelection(value);
        }
    }
    private void SetSelection(SecretEntry? entry)
    {
        _selectedSecret = entry; OnPropertyChanged(nameof(SelectedSecret)); LoadEditFields(entry);
    }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) FilterSecrets(); } }
    public string EditName { get => _editName; set => SetProperty(ref _editName, value); }
    public string EditValue { get => _editValue; set => SetProperty(ref _editValue, value); }
    public string EditCategory { get => _editCategory; set => SetProperty(ref _editCategory, value); }
    public string EditNotes { get => _editNotes; set => SetProperty(ref _editNotes, value); }
    public bool IsValueVisible { get => _isValueVisible; set => SetProperty(ref _isValueVisible, value); }
    public bool IsLocked { get => _isLocked; private set => SetProperty(ref _isLocked, value); }
    public bool IsBusy { get => _isBusy; private set { SetProperty(ref _isBusy, value); RelayCommand.RaiseCanExecuteChanged(); } }
    public string MasterPassword { get => _masterPassword; set => SetProperty(ref _masterPassword, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    private string Draft => JsonSerializer.Serialize(new[] { EditName, EditValue, EditCategory, EditNotes });
    private bool ConfirmDiscard() => IsLocked || Draft == _draftBaseline || _dialogs.Confirm("Unsaved secret", "Discard the unsaved secret changes?");

    public AsyncRelayCommand UnlockCommand { get; }
    public RelayCommand LockCommand { get; }
    public RelayCommand AddSecretCommand { get; }
    public AsyncRelayCommand SaveSecretCommand { get; }
    public AsyncRelayCommand DeleteSecretCommand { get; }
    public RelayCommand CopyValueCommand { get; }
    public RelayCommand ToggleVisibilityCommand { get; }

    public LocalSecretVaultViewModel(IClipboardService clipboard) : this(clipboard, null) { }
    internal LocalSecretVaultViewModel(IClipboardService clipboard, string? vaultPath, IUserDialogService? dialogs = null)
    {
        _vaultPath = Path.GetFullPath(vaultPath ?? Path.Combine(AppDataPaths.LocalDirectory, "vault.json"));
        _allowLegacyMigration = vaultPath is null && !AppDataPaths.IsOverridden;
        _clipboard = clipboard; _dialogs = dialogs ?? new UserDialogService();
        _draftBaseline = Draft;
        UnlockCommand = new AsyncRelayCommand(UnlockAsync, () => IsLocked && !IsBusy && !_disposed);
        LockCommand = new RelayCommand(() => { if (!IsBusy && ConfirmDiscard()) Lock(); }, () => !IsLocked && !IsBusy);
        AddSecretCommand = new RelayCommand(() => { if (!IsBusy && !IsLocked && ConfirmDiscard()) SetSelection(null); }, () => !IsLocked && !IsBusy);
        SaveSecretCommand = new AsyncRelayCommand(SaveSecretAsync, () => !IsLocked && !IsBusy);
        DeleteSecretCommand = new AsyncRelayCommand(DeleteSecretAsync, () => !IsLocked && !IsBusy && SelectedSecret is not null);
        CopyValueCommand = new RelayCommand(CopyValue, () => !IsLocked && !IsBusy && SelectedSecret is not null);
        ToggleVisibilityCommand = new RelayCommand(() => IsValueVisible = !IsValueVisible, () => !IsLocked && !IsBusy);
    }

    internal async Task UnlockAsync()
    {
        if (IsBusy || !IsLocked || _disposed) return;
        if (string.IsNullOrWhiteSpace(MasterPassword)) { StatusMessage = "Please enter a master password."; return; }
        if (_lockedOutUntilUtc > DateTimeOffset.UtcNow)
        {
            StatusMessage = $"Too many failed attempts. Try again in {Math.Ceiling((_lockedOutUntilUtc.Value - DateTimeOffset.UtcNow).TotalSeconds)}s.";
            return;
        }
        IsBusy = true;
        var candidate = new SecretVaultStore(_vaultPath);
        var password = MasterPassword;
        MasterPassword = "";
        try
        {
            var entries = await Task.Run(() => { MigrateLegacyVaultIfNeeded(); return candidate.Unlock(password); });
            if (_disposed) return;
            _store?.Dispose(); _store = candidate; candidate = null!;
            _allSecrets = entries; IsLocked = false;
            _failedUnlockAttempts = 0; _lockedOutUntilUtc = null;
            SetSelection(null); FilterSecrets();
            StatusMessage = $"Vault unlocked. {entries.Count} secret(s) loaded.";
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            _failedUnlockAttempts++;
            _lockedOutUntilUtc = DateTimeOffset.UtcNow + (_failedUnlockAttempts >= 10 ? TimeSpan.FromMinutes(2) : GetUnlockBackoffDelay(_failedUnlockAttempts));
            StatusMessage = "Incorrect master password or damaged vault. The file was left unchanged." + BackupHint();
        }
        catch (Exception ex) { StatusMessage = $"Unlock failed: {ex.Message}" + BackupHint(); }
        finally { candidate?.Dispose(); IsBusy = false; if (_disposed) Lock(); }
    }

    private string BackupHint() => File.Exists(_vaultPath + ".bak") ? $" An encrypted backup is available at {_vaultPath}.bak." : "";
    private void Lock()
    {
        _store?.Dispose(); _store = null; IsLocked = true;
        _updatingSelection = true;
        try { Secrets.Clear(); _allSecrets.Clear(); SetSelection(null); }
        finally { _updatingSelection = false; }
        SearchText = MasterPassword = ""; IsValueVisible = false; StatusMessage = "Vault locked.";
    }

    internal async Task SaveSecretAsync()
    {
        if (IsBusy || IsLocked || _store is null) return;
        if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Enter a name for the secret."; return; }
        IsBusy = true;
        try
        {
            var entry = new SecretEntry
            {
                Id = SelectedSecret?.Id ?? Guid.NewGuid().ToString(), Name = EditName, Category = EditCategory, Notes = EditNotes,
                EncryptedValue = _store.EncryptValue(EditValue), CreatedAt = SelectedSecret?.CreatedAt ?? DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            var submittedDraft = Draft;
            var entries = _allSecrets.Where(s => s.Id != entry.Id).Append(entry).ToList();
            var store = _store;
            await Task.Run(() => store.Save(entries));
            if (_disposed) return;
            _allSecrets = entries; FilterSecrets();
            _selectedSecret = entry; OnPropertyChanged(nameof(SelectedSecret));
            _draftBaseline = submittedDraft;
            StatusMessage = "Secret saved.";
        }
        catch (Exception ex) { StatusMessage = $"Save failed; the previous vault and saved entries were preserved: {ex.Message}"; }
        finally { IsBusy = false; if (_disposed) Lock(); }
    }

    internal async Task DeleteSecretAsync()
    {
        if (IsBusy || IsLocked || SelectedSecret is null || _store is null) return;
        if (!_dialogs.Confirm("Delete secret", $"Delete '{SelectedSecret.Name}' from the vault?")) return;
        IsBusy = true;
        try
        {
            var entries = _allSecrets.Where(s => s.Id != SelectedSecret.Id).ToList();
            var store = _store;
            await Task.Run(() => store.Save(entries));
            if (_disposed) return;
            _allSecrets = entries; SetSelection(null); FilterSecrets();
            StatusMessage = "Secret deleted.";
        }
        catch (Exception ex) { StatusMessage = $"Delete failed; the saved entry was preserved: {ex.Message}"; }
        finally { IsBusy = false; if (_disposed) Lock(); }
    }

    private void CopyValue()
    {
        if (IsLocked || _store is null || SelectedSecret is null) return;
        try { _clipboard.SetText(_store.DecryptValue(SelectedSecret.EncryptedValue)); StatusMessage = "Value copied to clipboard."; }
        catch (Exception ex) { StatusMessage = $"Could not copy the value: {ex.Message}"; }
    }
    private void LoadEditFields(SecretEntry? entry)
    {
        EditName = entry?.Name ?? ""; EditCategory = entry?.Category ?? ""; EditNotes = entry?.Notes ?? "";
        EditValue = ""; IsValueVisible = false;
        try { if (entry is not null && _store is not null) EditValue = _store.DecryptValue(entry.EncryptedValue); }
        catch { StatusMessage = "The selected secret could not be decrypted."; }
        _draftBaseline = Draft;
    }
    private void FilterSecrets()
    {
        var updating = _updatingSelection; _updatingSelection = true;
        try
        {
            Secrets.Clear();
            foreach (var entry in _allSecrets.Where(s => s.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) || s.Category.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)))
                Secrets.Add(entry);
            OnPropertyChanged(nameof(SelectedSecret));
        }
        finally { _updatingSelection = updating; }
    }
    private void MigrateLegacyVaultIfNeeded()
    {
        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsUtilityPack", "vault.json");
        if (!_allowLegacyMigration || File.Exists(_vaultPath) || !File.Exists(legacy)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(_vaultPath)!);
        File.Copy(legacy, _vaultPath, overwrite: false);
    }
    internal static TimeSpan GetUnlockBackoffDelay(int failedAttempts) =>
        TimeSpan.FromMilliseconds(Math.Min(10_000, 250 * (int)Math.Pow(2, Math.Clamp(failedAttempts - 1, 0, 6))));
    internal static void ClearSensitiveBuffer(byte[]? buffer) { if (buffer is not null) CryptographicOperations.ZeroMemory(buffer); }
    public bool CanNavigateAway() => !IsBusy && ConfirmDiscard();
    public void Activate() { }
    public void Deactivate() => Lock();
    public void Dispose() { _disposed = true; if (!IsBusy) Lock(); }
}