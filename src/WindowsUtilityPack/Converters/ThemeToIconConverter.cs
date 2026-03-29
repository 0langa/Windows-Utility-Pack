using System.Globalization;
using System.Windows.Data;
using WindowsUtilityPack.Services;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Converts an AppTheme enum value to a display icon string.
/// </summary>
[ValueConversion(typeof(AppTheme), typeof(string))]
public class ThemeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is AppTheme theme && theme == AppTheme.Dark ? "☀" : "🌙";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
