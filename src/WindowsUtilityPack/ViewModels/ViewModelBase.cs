using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// Base class for all ViewModels. Implements INotifyPropertyChanged for data binding.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises PropertyChanged for the given property name.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets a backing field and raises PropertyChanged if the value changed.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
