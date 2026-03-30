using System.Globalization;
using System.Windows.Data;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Negates a boolean value: <see langword="true"/> → <see langword="false"/> and vice versa.
/// Useful for binding <c>IsEnabled</c> to an inverted boolean property (e.g. <c>IsBusy</c>).
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}
