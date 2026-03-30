using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace WindowsUtilityPack.ViewModels;

/// <summary>
/// Base class for all ViewModels in the application.
/// Implements <see cref="INotifyPropertyChanged"/> so WPF data bindings
/// update automatically when bound properties change.
///
/// Usage: inherit from this class and call <see cref="SetProperty{T}"/>
/// inside property setters instead of manually raising <see cref="PropertyChanged"/>.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    private static readonly ConcurrentDictionary<string, PropertyChangedEventArgs> PropertyChangedEventArgsCache = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the given property.
    /// Typically called by <see cref="SetProperty{T}"/> automatically;
    /// can also be called manually for computed/derived properties.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, GetPropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets a backing field to <paramref name="value"/> and raises <see cref="PropertyChanged"/>
    /// if the value actually changed.  Returns <see langword="true"/> if the change occurred.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">The new value to assign.</param>
    /// <param name="propertyName">Auto-filled by the compiler with the caller's member name.</param>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private static PropertyChangedEventArgs GetPropertyChangedEventArgs(string? propertyName)
    {
        if (propertyName is null)
        {
            return new PropertyChangedEventArgs(null);
        }

        return PropertyChangedEventArgsCache.GetOrAdd(propertyName, static name => new PropertyChangedEventArgs(name));
    }
}
