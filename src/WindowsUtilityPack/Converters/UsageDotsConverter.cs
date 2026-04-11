using System.Globalization;
using System.Windows.Data;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Converts a (string toolKey, IReadOnlyDictionary&lt;string,int&gt; counts) pair
/// to a small usage-frequency dot string: "" / "·" / "··" / "···".
/// Used in the All Tools card grid to show relative usage frequency.
/// </summary>
public sealed class UsageDotsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return string.Empty;

        var key = values[0] as string;
        var counts = values[1] as IReadOnlyDictionary<string, int>;

        if (key is null || counts is null) return string.Empty;
        if (!counts.TryGetValue(key, out var count) || count <= 0) return string.Empty;

        return count switch
        {
            <= 3  => "·",
            <= 15 => "··",
            _     => "···",
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
