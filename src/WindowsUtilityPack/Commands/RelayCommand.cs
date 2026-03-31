using System.Windows.Input;

namespace WindowsUtilityPack.Commands;

/// <summary>
/// A general-purpose <see cref="ICommand"/> implementation that delegates its
/// <see cref="Execute"/> and <see cref="CanExecute"/> logic via delegates,
/// enabling easy MVVM command binding without creating a dedicated command class
/// for every action.
///
/// <c>CanExecuteChanged</c> is hooked to <c>CommandManager.RequerySuggested</c>
/// so WPF automatically re-evaluates command availability after UI interactions.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>
    /// Initialises a new <see cref="RelayCommand"/> with the given execute action
    /// and an optional predicate that controls whether the command is enabled.
    /// </summary>
    /// <param name="execute">The action to run when the command executes.  Must not be null.</param>
    /// <param name="canExecute">
    /// Optional predicate returning <see langword="true"/> when the command should be enabled.
    /// When <see langword="null"/>, the command is always enabled.
    /// </param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Creates a parameterless relay command.
    /// </summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(
              _ => execute(),
              canExecute != null ? _ => canExecute() : null)
    {
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    /// <summary>
    /// Forces WPF to re-evaluate <see cref="CanExecute"/> on all bound commands
    /// by invalidating the CommandManager's re-query cache.
    /// Call this when external state that affects <c>canExecute</c> changes.
    /// </summary>
    public static void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
