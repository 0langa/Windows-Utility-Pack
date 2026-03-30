using System.Globalization;
using System.Windows.Data;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Converts a percentage (0-100 double) and a total available width (double ConverterParameter)
/// to a pixel width, for rendering proportional usage bars in the Storage Master treemap/overview.
/// Usage: Width="{Binding UsedPercent, Converter={StaticResource PercentageToWidthConverter}, ConverterParameter=300}"
/// </summary>
[ValueConversion(typeof(double), typeof(double))]
public class PercentageToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double pct) return 0.0;
        double totalWidth = parameter is string s && double.TryParse(s, out var w) ? w : 200.0;
        return Math.Max(0, Math.Min(totalWidth, pct / 100.0 * totalWidth));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
