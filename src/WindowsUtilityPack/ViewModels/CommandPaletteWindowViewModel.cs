using System.Collections.ObjectModel;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// ViewModel for the detached shell command palette window.
/// </summary>
public sealed class CommandPaletteWindowViewModel : ViewModelBase
{
    private readonly ICommandPaletteService _palette;
    private string _query = string.Empty;
    private CommandPaletteItem? _selectedItem;

    public event EventHandler<CommandPaletteItem>? ExecuteRequested;

    public ObservableCollection<CommandPaletteItem> Items { get; } = [];

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                Refresh();
            }
        }
    }

    public CommandPaletteItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public RelayCommand ExecuteSelectedCommand { get; }

    public CommandPaletteWindowViewModel(ICommandPaletteService palette)
    {
        _palette = palette ?? throw new ArgumentNullException(nameof(palette));
        ExecuteSelectedCommand = new RelayCommand(_ => RequestExecuteSelected(), _ => SelectedItem is not null);
        Refresh();
    }

    public void ActivateFresh()
    {
        Query = string.Empty;
        Refresh();
    }

    public void RequestExecuteSelected()
    {
        if (SelectedItem is null)
        {
            return;
        }

        ExecuteRequested?.Invoke(this, SelectedItem);
    }

    public void Refresh()
    {
        var matches = _palette.Search(Query, limit: 40);
        Items.Clear();
        foreach (var match in matches)
        {
            Items.Add(match);
        }

        SelectedItem = Items.FirstOrDefault();
        RelayCommand.RaiseCanExecuteChanged();
    }
}
