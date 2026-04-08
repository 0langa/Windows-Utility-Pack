using System.Globalization;
using System.Windows.Data;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Converts percentage values (e.g. 125) into scale factors (1.25).
/// </summary>
public sealed class PercentageToScaleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return 1d;
        }

        if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
        {
            return percentage / 100d;
        }

        return 1d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return 100d;
        }

        if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
        {
            return scale * 100d;
        }

        return 100d;
    }
}
