using System.Security.Cryptography;
using System.Text;
using System.Windows;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SecurityPrivacy.PasswordGenerator;

public class PasswordGeneratorViewModel : ViewModelBase
{
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}|;:,.<>?";

    private int _length = 16;
    private bool _useUppercase = true;
    private bool _useLowercase = true;
    private bool _useDigits = true;
    private bool _useSymbols = false;
    private string _generatedPassword = string.Empty;
    private string _strengthLabel = string.Empty;

    public int Length
    {
        get => _length;
        set { if (SetProperty(ref _length, value)) Generate(); }
    }

    public bool UseUppercase
    {
        get => _useUppercase;
        set { if (SetProperty(ref _useUppercase, value)) Generate(); }
    }

    public bool UseLowercase
    {
        get => _useLowercase;
        set { if (SetProperty(ref _useLowercase, value)) Generate(); }
    }

    public bool UseDigits
    {
        get => _useDigits;
        set { if (SetProperty(ref _useDigits, value)) Generate(); }
    }

    public bool UseSymbols
    {
        get => _useSymbols;
        set { if (SetProperty(ref _useSymbols, value)) Generate(); }
    }

    public string GeneratedPassword
    {
        get => _generatedPassword;
        private set { SetProperty(ref _generatedPassword, value); UpdateStrength(); }
    }

    public string StrengthLabel
    {
        get => _strengthLabel;
        private set => SetProperty(ref _strengthLabel, value);
    }

    public RelayCommand GenerateCommand { get; }
    public RelayCommand CopyCommand { get; }

    public PasswordGeneratorViewModel()
    {
        GenerateCommand = new RelayCommand(_ => Generate());
        CopyCommand = new RelayCommand(_ => CopyToClipboard(), _ => !string.IsNullOrEmpty(GeneratedPassword));
        Generate();
    }

    private void Generate()
    {
        var pool = new StringBuilder();
        if (UseUppercase) pool.Append(Uppercase);
        if (UseLowercase) pool.Append(Lowercase);
        if (UseDigits) pool.Append(Digits);
        if (UseSymbols) pool.Append(Symbols);

        if (pool.Length == 0) { GeneratedPassword = string.Empty; return; }

        var chars = pool.ToString();
        var result = new char[Length];
        for (var i = 0; i < Length; i++)
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        GeneratedPassword = new string(result);
    }

    private void UpdateStrength()
    {
        var score = 0;
        if (UseUppercase) score++;
        if (UseLowercase) score++;
        if (UseDigits) score++;
        if (UseSymbols) score++;
        if (Length >= 12) score++;
        if (Length >= 20) score++;

        StrengthLabel = score switch
        {
            <= 2 => "Weak",
            3 or 4 => "Fair",
            5 => "Strong",
            _ => "Very Strong"
        };
    }

    private void CopyToClipboard()
    {
        if (!string.IsNullOrEmpty(GeneratedPassword))
            Clipboard.SetText(GeneratedPassword);
    }
}
