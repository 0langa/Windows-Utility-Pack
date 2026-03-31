using System.Globalization;
using System.Windows.Data;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Converts an enum value to <see langword="true"/> when it matches the converter parameter,
/// enabling two-way RadioButton ↔ enum binding.
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.Equals(parameter) == true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true && parameter is not null ? parameter : Binding.DoNothing;
}
