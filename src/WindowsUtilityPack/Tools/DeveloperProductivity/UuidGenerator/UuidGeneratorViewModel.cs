using System.Collections.ObjectModel;
using System.Text;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services.Identifier;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.UuidGenerator;

public class GeneratedId
{
    public string Id { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}

public sealed class UuidGeneratorViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboardService;
    private readonly IUlidGenerator _ulidGenerator;

    private string _idType = "UUID";
    private string _format = "Standard";
    private int _generateCount = 5;
    private string _singleId = string.Empty;

    private static readonly string[] UuidFormats = ["Standard", "No Hyphens", "Uppercase", "Braces {}", "URN Prefix", "Lowercase"];
    private static readonly string[] UlidFormats = ["Uppercase", "Lowercase"];

    public ObservableCollection<string> IdTypes { get; } = ["UUID", "ULID"];
    public ObservableCollection<string> Formats { get; } = [];

    public string IdType
    {
        get => _idType;
        set
        {
            if (SetProperty(ref _idType, value))
            {
                RefreshFormats();
                GenerateSingle();
            }
        }
    }

    public string Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    public int GenerateCount
    {
        get => _generateCount;
        set => SetProperty(ref _generateCount, Math.Max(1, Math.Min(1000, value)));
    }

    public ObservableCollection<GeneratedId> GeneratedIds { get; } = [];

    public string SingleId
    {
        get => _singleId;
        private set => SetProperty(ref _singleId, value);
    }

    public RelayCommand GenerateSingleCommand { get; }
    public RelayCommand GenerateBulkCommand { get; }
    public RelayCommand CopySingleCommand { get; }
    public RelayCommand CopyAllCommand { get; }
    public RelayCommand ClearCommand { get; }

    public UuidGeneratorViewModel(IClipboardService clipboardService, IUlidGenerator ulidGenerator)
    {
        _clipboardService = clipboardService;
        _ulidGenerator = ulidGenerator;
        RefreshFormats();

        GenerateSingleCommand = new RelayCommand(_ => GenerateSingle());
        GenerateBulkCommand = new RelayCommand(_ => GenerateBulk());
        CopySingleCommand = new RelayCommand(_ => _clipboardService.SetText(SingleId),
                                             _ => !string.IsNullOrEmpty(SingleId));
        CopyAllCommand = new RelayCommand(_ => CopyAll(), _ => GeneratedIds.Count > 0);
        ClearCommand = new RelayCommand(_ => { GeneratedIds.Clear(); SingleId = string.Empty; });

        GenerateSingle();
    }

    private void RefreshFormats()
    {
        Formats.Clear();
        foreach (var item in IdType == "ULID" ? UlidFormats : UuidFormats)
            Formats.Add(item);

        if (!Formats.Contains(Format))
            Format = Formats[0];
    }

    private string ApplyFormat(Guid guid, string ulid)
    {
        if (IdType == "ULID")
        {
            return Format switch
            {
                "Lowercase" => ulid.ToLowerInvariant(),
                _ => ulid.ToUpperInvariant(),
            };
        }

        return Format switch
        {
            "No Hyphens"  => guid.ToString("N"),
            "Uppercase"   => guid.ToString().ToUpperInvariant(),
            "Braces {}"   => guid.ToString("B"),
            "URN Prefix"  => "urn:uuid:" + guid.ToString(),
            "Lowercase"   => guid.ToString().ToLowerInvariant(),
            _             => guid.ToString()
        };
    }

    private void GenerateSingle()
    {
        var guid = Guid.NewGuid();
        var ulid = _ulidGenerator.Generate();
        SingleId = ApplyFormat(guid, ulid);
    }

    private void GenerateBulk()
    {
        GeneratedIds.Clear();
        for (int i = 0; i < GenerateCount; i++)
        {
            var guid = Guid.NewGuid();
            var ulid = _ulidGenerator.Generate();
            GeneratedIds.Add(new GeneratedId
            {
                Id = ApplyFormat(guid, ulid),
                GeneratedAt = DateTime.Now
            });
        }
    }

    private void CopyAll()
    {
        var sb = new StringBuilder();
        foreach (var item in GeneratedIds)
            sb.AppendLine(item.Id);
        _clipboardService.SetText(sb.ToString().TrimEnd());
    }
}
