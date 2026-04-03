using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WindowsUtilityPack.Commands;

/// <summary>
/// An async relay command that supports both parameterless and parameterized delegates.
///
/// <para>
/// Exceptions thrown by the async body surface through the optional
/// <paramref name="onException"/> callback, giving call sites a consistent
/// reporting/logging hook without requiring every command body to handle its own
/// exceptions.  If no callback is supplied any exception propagates via the
/// <c>async void</c> bridge and will be raised on the WPF dispatcher (same
/// behaviour as before).
/// </para>
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<Exception>? _onException;
    private bool _isExecuting;

    /// <summary>
    /// Creates a parameterless async command.
    /// </summary>
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onException = null)
        : this(
              _ => execute(),
              canExecute != null ? _ => canExecute() : null,
              onException)
    {
    }

    /// <summary>
    /// Creates a parameterized async command.
    /// </summary>
    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null, Action<Exception>? onException = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onException = onException;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _execute(parameter);
            }
            catch (Exception ex) when (_onException != null)
            {
                _onException(ex);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
