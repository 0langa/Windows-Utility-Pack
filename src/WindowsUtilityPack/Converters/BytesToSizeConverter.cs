using System.Globalization;
using System.Windows.Data;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Converts a long (byte count) to a human-readable size string ("1.2 GB", etc.).
/// Used in Storage Master file list and tree view bindings.
/// </summary>
[ValueConversion(typeof(long), typeof(string))]
public class BytesToSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is long bytes ? StorageItem.FormatBytes(bytes) : "—";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
