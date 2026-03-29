using System.Windows.Input;

namespace WindowsUtilityPack.Commands;

/// <summary>
/// An async-aware <see cref="ICommand"/> implementation for use with
/// <c>async</c>/<c>await</c> patterns in ViewModels.
///
/// Key behaviours:
/// <list type="bullet">
///   <item>Prevents re-entrant execution — <see cref="CanExecute"/> returns
///         <see langword="false"/> while an async operation is in progress.</item>
///   <item>Automatically refreshes command state before and after execution.</item>
///   <item>Exceptions propagate to the WPF dispatcher (same as synchronous commands).</item>
/// </list>
///
/// Use this instead of <see cref="RelayCommand"/> whenever the command body
/// needs to <c>await</c> anything (network calls, file I/O, etc.).
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;

    // Tracks execution state to prevent re-entrancy.
    private bool _isExecuting;

    /// <summary>
    /// Initialises a new <see cref="AsyncRelayCommand"/>.
    /// </summary>
    /// <param name="execute">Async action to run.  Must not be null.</param>
    /// <param name="canExecute">
    /// Optional predicate; also checked against <c>_isExecuting</c> so the
    /// command is automatically disabled while the previous call is running.
    /// </param>
    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc/>
    /// <remarks>Returns <see langword="false"/> while a prior execution is still running.</remarks>
    public bool CanExecute(object? parameter) =>
        !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    /// <inheritdoc/>
    /// <remarks>
    /// Executes the async action on the calling (UI) thread's synchronisation context.
    /// The <c>async void</c> signature is intentional — WPF commands are fire-and-forget
    /// by design; unhandled exceptions surface via <c>Application.DispatcherUnhandledException</c>.
    /// </remarks>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();  // Disable the button during execution.

        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();  // Re-enable when done.
        }
    }
}
