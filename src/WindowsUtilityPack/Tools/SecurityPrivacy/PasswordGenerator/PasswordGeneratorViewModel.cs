using System.Security.Cryptography;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SecurityPrivacy.PasswordGenerator;

/// <summary>
/// ViewModel for the Password Generator tool.
///
/// Generates cryptographically random passwords using <see cref="RandomNumberGenerator.GetInt32"/>
/// (not <see cref="System.Random"/>) so passwords are suitable for actual security use.
///
/// The password is regenerated automatically whenever any option changes (length,
/// character set toggles), so the UI always reflects the current configuration.
/// </summary>
public class PasswordGeneratorViewModel : ViewModelBase
{
    // Available character pools.
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits    = "0123456789";
    private const string Symbols   = "!@#$%^&*()-_=+[]{}|;:,.<>?";

    private readonly IClipboardService _clipboard;

    private int    _length            = 16;
    private bool   _useUppercase      = true;
    private bool   _useLowercase      = true;
    private bool   _useDigits         = true;
    private bool   _useSymbols        = false;
    private string _generatedPassword = string.Empty;
    private string _strengthLabel     = string.Empty;

    /// <summary>Desired password length (regenerates on change).</summary>
    public int Length
    {
        get => _length;
        set { if (SetProperty(ref _length, value)) Generate(); }
    }

    /// <summary>Include A–Z uppercase letters.</summary>
    public bool UseUppercase
    {
        get => _useUppercase;
        set { if (SetProperty(ref _useUppercase, value)) Generate(); }
    }

    /// <summary>Include a–z lowercase letters.</summary>
    public bool UseLowercase
    {
        get => _useLowercase;
        set { if (SetProperty(ref _useLowercase, value)) Generate(); }
    }

    /// <summary>Include 0–9 digits.</summary>
    public bool UseDigits
    {
        get => _useDigits;
        set { if (SetProperty(ref _useDigits, value)) Generate(); }
    }

    /// <summary>Include special symbol characters.</summary>
    public bool UseSymbols
    {
        get => _useSymbols;
        set { if (SetProperty(ref _useSymbols, value)) Generate(); }
    }

    /// <summary>The most recently generated password string.</summary>
    public string GeneratedPassword
    {
        get => _generatedPassword;
        private set { SetProperty(ref _generatedPassword, value); UpdateStrength(); }
    }

    /// <summary>
    /// Qualitative strength label ("Weak", "Fair", "Strong", "Very Strong")
    /// computed from the active character sets and password length.
    /// </summary>
    public string StrengthLabel
    {
        get => _strengthLabel;
        private set => SetProperty(ref _strengthLabel, value);
    }

    /// <summary>Generates a new random password using the current settings.</summary>
    public RelayCommand GenerateCommand { get; }

    /// <summary>Copies <see cref="GeneratedPassword"/> to the system clipboard.  Disabled when empty.</summary>
    public RelayCommand CopyCommand { get; }

    public PasswordGeneratorViewModel()
        : this(new ClipboardService()) { }

    /// <summary>Constructor used in tests or custom wiring with an injected clipboard service.</summary>
    public PasswordGeneratorViewModel(IClipboardService clipboard)
    {
        _clipboard      = clipboard;
        GenerateCommand = new RelayCommand(_ => Generate());
        CopyCommand     = new RelayCommand(
            _ => CopyToClipboard(),
            _ => !string.IsNullOrEmpty(GeneratedPassword));

        // Generate an initial password immediately.
        Generate();
    }

    /// <summary>
    /// Builds the character pool from the active options and draws <see cref="Length"/>
    /// characters using <see cref="RandomNumberGenerator.GetInt32"/> for cryptographic randomness.
    ///
    /// To guarantee that every enabled character class appears at least once in the output,
    /// one random character from each active pool is placed at a random position first,
    /// then the remaining positions are filled from the combined pool, and the entire
    /// array is shuffled (Fisher-Yates) to avoid a predictable prefix.
    ///
    /// Sets <see cref="GeneratedPassword"/> to empty string if no character sets are selected.
    /// </summary>
    private void Generate()
    {
        // Collect the active pools.
        var pools = new List<string>();
        if (UseUppercase) pools.Add(Uppercase);
        if (UseLowercase) pools.Add(Lowercase);
        if (UseDigits)    pools.Add(Digits);
        if (UseSymbols)   pools.Add(Symbols);

        if (pools.Count == 0) { GeneratedPassword = string.Empty; return; }

        var combinedPool = string.Concat(pools);
        var result = new char[Length];

        // Step 1: guarantee at least one character from each active pool.
        var guaranteedCount = Math.Min(pools.Count, Length);
        for (var i = 0; i < guaranteedCount; i++)
        {
            var pool = pools[i];
            result[i] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }

        // Step 2: fill remaining positions from the combined pool.
        for (var i = guaranteedCount; i < Length; i++)
            result[i] = combinedPool[RandomNumberGenerator.GetInt32(combinedPool.Length)];

        // Step 3: Fisher-Yates shuffle to remove positional bias.
        for (var i = result.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        GeneratedPassword = new string(result);
    }

    /// <summary>
    /// Computes a simple strength score (0–6) based on active character sets and length.
    /// Each active set adds 1 point; lengths ≥ 12 and ≥ 20 each add another point.
    /// </summary>
    private void UpdateStrength()
    {
        var score = 0;
        if (UseUppercase) score++;
        if (UseLowercase) score++;
        if (UseDigits)    score++;
        if (UseSymbols)   score++;
        if (Length >= 12) score++;
        if (Length >= 20) score++;

        StrengthLabel = score switch
        {
            <= 2    => "Weak",
            3 or 4  => "Fair",
            5       => "Strong",
            _       => "Very Strong"
        };
    }

    private void CopyToClipboard()
    {
        if (!string.IsNullOrEmpty(GeneratedPassword))
            _clipboard.SetText(GeneratedPassword);
    }
}
