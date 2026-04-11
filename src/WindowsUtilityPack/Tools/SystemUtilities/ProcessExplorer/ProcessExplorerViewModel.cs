using System.Collections.ObjectModel;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.ProcessExplorer;

/// <summary>
/// ViewModel for process explorer capabilities.
/// </summary>
public sealed class ProcessExplorerViewModel : ViewModelBase
{
    private readonly IProcessExplorerService _service;
    private readonly IUserDialogService _dialogs;
    private readonly IClipboardService _clipboard;

    private ProcessSnapshot? _selectedProcess;
    private string _searchQuery = string.Empty;
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    public ObservableCollection<ProcessSnapshot> Processes { get; } = [];

    public ProcessSnapshot? SelectedProcess
    {
        get => _selectedProcess;
        set => SetProperty(ref _selectedProcess, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand TerminateCommand { get; }
    public AsyncRelayCommand CopyDetailsCommand { get; }

    public ProcessExplorerViewModel(IProcessExplorerService service, IUserDialogService dialogs, IClipboardService clipboard)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        TerminateCommand = new AsyncRelayCommand(_ => TerminateAsync(), _ => SelectedProcess is not null);
        CopyDetailsCommand = new AsyncRelayCommand(_ => CopyDetailsAsync(), _ => SelectedProcess is not null);

        _ = RefreshAsync();
    }

    internal async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var rows = await _service.GetProcessesAsync(SearchQuery).ConfigureAwait(true);

            Processes.Clear();
            foreach (var row in rows)
            {
                Processes.Add(row);
            }

            StatusMessage = $"Loaded {Processes.Count:N0} processes.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to load processes.";
            _dialogs.ShowError("Process Explorer", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task CopyDetailsAsync()
    {
        if (SelectedProcess is null)
        {
            return;
        }

        try
        {
            var details = await _service.BuildDetailsAsync(SelectedProcess.ProcessId).ConfigureAwait(true);
            _clipboard.SetText(details);
            StatusMessage = "Process details copied to clipboard.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to copy process details.";
            _dialogs.ShowError("Process Explorer", ex.Message);
        }
    }

    internal async Task TerminateAsync()
    {
        if (SelectedProcess is null)
        {
            return;
        }

        var target = SelectedProcess;
        if (!_dialogs.Confirm("Terminate process", $"Terminate '{target.Name}' (PID {target.ProcessId})?"))
        {
            return;
        }

        var success = await _service.TryTerminateAsync(target.ProcessId).ConfigureAwait(true);
        if (!success)
        {
            StatusMessage = "Unable to terminate selected process.";
            _dialogs.ShowError("Process Explorer", "The selected process could not be terminated.");
            return;
        }

        StatusMessage = $"Terminated '{target.Name}' (PID {target.ProcessId}).";
        await RefreshAsync().ConfigureAwait(true);
    }
}