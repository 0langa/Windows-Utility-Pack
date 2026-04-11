using System.Collections.ObjectModel;
using System.Windows.Threading;
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

    // For live refresh
    private readonly DispatcherTimer _refreshTimer;
    private int _refreshIntervalSeconds = 2;
    private DateTime _lastRefreshTime = DateTime.MinValue;
    private List<ProcessSnapshot> _previousSnapshots = new();
    private readonly int _processorCount = Environment.ProcessorCount;
    private bool _autoRefreshEnabled = true;

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

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set
        {
            if (value < 1) value = 1;
            if (SetProperty(ref _refreshIntervalSeconds, value))
            {
                _refreshTimer.Interval = TimeSpan.FromSeconds(_refreshIntervalSeconds);
            }
        }
    }

    public bool AutoRefreshEnabled
    {
        get => _autoRefreshEnabled;
        set
        {
            if (SetProperty(ref _autoRefreshEnabled, value))
            {
                _refreshTimer.IsEnabled = value;
            }
        }
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

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_refreshIntervalSeconds) };
        _refreshTimer.Tick += async (s, e) => { if (_autoRefreshEnabled) await RefreshAsync(); };
        _refreshTimer.Start();

        _ = RefreshAsync();
    }

    internal async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var now = DateTime.UtcNow;
            var elapsed = _lastRefreshTime == DateTime.MinValue ? 1.0 : (now - _lastRefreshTime).TotalSeconds;
            _lastRefreshTime = now;

            var rows = (await _service.GetProcessesAsync(SearchQuery).ConfigureAwait(true)).ToList();

            // Compute CPU% for each process by matching with previous snapshot
            foreach (var row in rows)
            {
                var prev = _previousSnapshots.FirstOrDefault(p => p.ProcessId == row.ProcessId);
                if (prev != null && elapsed > 0)
                {
                    var cpuDelta = row.CpuTimeSeconds - prev.CpuTimeSeconds;
                    row.CpuPercent = Math.Max(0, Math.Min(100, (cpuDelta / elapsed / _processorCount) * 100));
                }
                else
                {
                    row.CpuPercent = 0;
                }
            }

            _previousSnapshots = rows.Select(p => new ProcessSnapshot
            {
                ProcessId = p.ProcessId,
                Name = p.Name,
                ExecutablePath = p.ExecutablePath,
                WorkingSetMb = p.WorkingSetMb,
                CpuTimeSeconds = p.CpuTimeSeconds,
                IsResponding = p.IsResponding,
                StartTimeLocal = p.StartTimeLocal,
                CpuPercent = p.CpuPercent
            }).ToList();

            // Sort by CPU% descending, then memory
            var sorted = rows.OrderByDescending(p => p.CpuPercent).ThenByDescending(p => p.WorkingSetMb).ToList();

            Processes.Clear();
            foreach (var row in sorted)
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