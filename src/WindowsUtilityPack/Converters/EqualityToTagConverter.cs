using System.Globalization;
using System.Windows.Data;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Multi-value converter that returns the string <c>"Selected"</c> when
/// the two bound values are reference-equal, otherwise <c>null</c>.
/// Used to drive <c>Tag</c>-based visual states on category tab buttons.
/// </summary>
internal sealed class EqualityToTagConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return null;

        return ReferenceEquals(values[0], values[1]) ? "Selected" : null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
