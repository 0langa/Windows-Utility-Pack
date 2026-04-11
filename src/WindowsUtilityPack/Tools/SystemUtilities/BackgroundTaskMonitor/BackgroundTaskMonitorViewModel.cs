using System.Collections.ObjectModel;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.BackgroundTaskMonitor;

/// <summary>
/// ViewModel for monitoring shared background tasks.
/// </summary>
public sealed class BackgroundTaskMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly IBackgroundTaskService _tasks;
    private BackgroundTaskInfo? _selectedTask;
    private string _statusMessage = "Monitoring shared background tasks.";

    public ObservableCollection<BackgroundTaskInfo> TaskItems { get; } = [];

    public BackgroundTaskInfo? SelectedTask
    {
        get => _selectedTask;
        set => SetProperty(ref _selectedTask, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand CancelSelectedCommand { get; }

    public BackgroundTaskMonitorViewModel(IBackgroundTaskService tasks)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _tasks.TaskChanged += OnTaskChanged;

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        CancelSelectedCommand = new RelayCommand(_ => CancelSelected(), _ => SelectedTask is { State: BackgroundTaskState.Running });

        _ = RefreshAsync();
    }

    private Task RefreshAsync()
    {
        var snapshot = _tasks.GetTasks()
            .OrderByDescending(t => t.StartedUtc)
            .ToList();

        TaskItems.Clear();
        foreach (var task in snapshot)
        {
            TaskItems.Add(task);
        }

        StatusMessage = TaskItems.Count == 0
            ? "No tracked background tasks yet."
            : $"Showing {TaskItems.Count:N0} task records.";

        if (SelectedTask is not null)
        {
            SelectedTask = TaskItems.FirstOrDefault(t => t.TaskId == SelectedTask.TaskId);
        }

        return Task.CompletedTask;
    }

    private void CancelSelected()
    {
        if (SelectedTask is not { State: BackgroundTaskState.Running } selected)
        {
            return;
        }

        var cancelled = _tasks.CancelTask(selected.TaskId, "Cancelled from task monitor.");
        StatusMessage = cancelled
            ? $"Cancellation requested for '{selected.Name}'."
            : "Unable to cancel selected task.";

        _ = RefreshAsync();
    }

    private void OnTaskChanged(object? sender, BackgroundTaskInfo e)
    {
        _ = RefreshAsync();
    }

    public void Dispose()
    {
        _tasks.TaskChanged -= OnTaskChanged;
    }
}