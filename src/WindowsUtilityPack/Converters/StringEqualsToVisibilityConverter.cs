using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Converts a string value to <see cref="Visibility"/> by comparing it to
/// the converter parameter. Visible when the value equals the parameter;
/// Collapsed otherwise.
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public class StringEqualsToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string ?? string.Empty;
        var target = parameter as string ?? string.Empty;
        return str.Equals(target, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
