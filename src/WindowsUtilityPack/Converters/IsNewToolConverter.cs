using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Converts a nullable <see cref="DateTime"/> (a tool's DateAdded) to
/// <see cref="Visibility.Visible"/> when the date is within the last 30 days,
/// or <see cref="Visibility.Collapsed"/> otherwise or when the value is null.
/// </summary>
public sealed class IsNewToolConverter : IValueConverter
{
    private static readonly TimeSpan NewWindow = TimeSpan.FromDays(30);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime added && DateTime.UtcNow - added.ToUniversalTime() <= NewWindow)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
