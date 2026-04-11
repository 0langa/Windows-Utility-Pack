using System.Collections.ObjectModel;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.TaskSchedulerUi;

/// <summary>
/// ViewModel for Task Scheduler query and run workflows.
/// </summary>
public sealed class TaskSchedulerUiViewModel : ViewModelBase
{
    private readonly ITaskSchedulerService _service;
    private readonly IUserDialogService _dialogs;

    private ScheduledTaskRow? _selectedTask;
    private string _searchQuery = string.Empty;
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    public ObservableCollection<ScheduledTaskRow> Tasks { get; } = [];

    public ScheduledTaskRow? SelectedTask
    {
        get => _selectedTask;
        set => SetProperty(ref _selectedTask, value);
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
    public AsyncRelayCommand RunSelectedCommand { get; }

    public TaskSchedulerUiViewModel(ITaskSchedulerService service, IUserDialogService dialogs)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        RunSelectedCommand = new AsyncRelayCommand(_ => RunSelectedAsync(), _ => SelectedTask is not null);

        _ = RefreshAsync();
    }

    internal async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var rows = await _service.GetTasksAsync(SearchQuery).ConfigureAwait(true);

            Tasks.Clear();
            foreach (var row in rows)
            {
                Tasks.Add(row);
            }

            StatusMessage = $"Loaded {Tasks.Count:N0} scheduled tasks.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to query scheduled tasks.";
            _dialogs.ShowError("Task Scheduler UI", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task RunSelectedAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        if (!_dialogs.Confirm("Run scheduled task", $"Run '{SelectedTask.TaskName}' now?"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var ok = await _service.RunTaskAsync(SelectedTask.TaskName).ConfigureAwait(true);
            if (!ok)
            {
                StatusMessage = "Unable to run selected task.";
                _dialogs.ShowError("Task Scheduler UI", "The selected task could not be started.");
                return;
            }

            StatusMessage = $"Task '{SelectedTask.TaskName}' started.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to run selected task.";
            _dialogs.ShowError("Task Scheduler UI", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}