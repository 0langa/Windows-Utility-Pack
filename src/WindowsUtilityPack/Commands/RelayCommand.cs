using System.Windows.Input;

namespace WindowsUtilityPack.Commands;

/// <summary>
/// A general-purpose ICommand implementation that delegates its Execute and CanExecute logic
/// via delegates, enabling easy MVVM command binding.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    /// <summary>
    /// Initializes a new RelayCommand with the given execute action and optional canExecute predicate.
    /// </summary>
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Forces a re-evaluation of CanExecute by raising the CanExecuteChanged event.
    /// </summary>
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
