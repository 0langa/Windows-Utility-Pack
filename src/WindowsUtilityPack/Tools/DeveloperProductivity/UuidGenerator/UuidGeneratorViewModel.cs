using System.Collections.ObjectModel;
using System.Text;
using WindowsUtilityPack.Commands;
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

    private string _format = "Standard";
    private int _generateCount = 5;
    private string _singleId = string.Empty;

    public ObservableCollection<string> Formats { get; } =
        ["Standard", "No Hyphens", "Uppercase", "Braces {}", "URN Prefix"];

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

    public UuidGeneratorViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;

        GenerateSingleCommand = new RelayCommand(_ => GenerateSingle());
        GenerateBulkCommand = new RelayCommand(_ => GenerateBulk());
        CopySingleCommand = new RelayCommand(_ => _clipboardService.SetText(SingleId),
                                             _ => !string.IsNullOrEmpty(SingleId));
        CopyAllCommand = new RelayCommand(_ => CopyAll(), _ => GeneratedIds.Count > 0);
        ClearCommand = new RelayCommand(_ => { GeneratedIds.Clear(); SingleId = string.Empty; });

        GenerateSingle();
    }

    private string ApplyFormat(Guid guid)
    {
        return Format switch
        {
            "No Hyphens"  => guid.ToString("N"),
            "Uppercase"   => guid.ToString().ToUpperInvariant(),
            "Braces {}"   => guid.ToString("B"),
            "URN Prefix"  => "urn:uuid:" + guid.ToString(),
            _             => guid.ToString()
        };
    }

    private void GenerateSingle()
    {
        SingleId = ApplyFormat(Guid.NewGuid());
    }

    private void GenerateBulk()
    {
        GeneratedIds.Clear();
        for (int i = 0; i < GenerateCount; i++)
        {
            GeneratedIds.Add(new GeneratedId
            {
                Id = ApplyFormat(Guid.NewGuid()),
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
