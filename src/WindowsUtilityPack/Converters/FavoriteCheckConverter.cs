using System.Globalization;
using System.Windows.Data;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Multi-value converter that returns <see langword="true"/> when a tool key
/// is found in the current favourites list.
/// <para>
/// Binding values:
/// <list type="number">
///   <item><c>Values[0]</c>: the tool key (<see cref="string"/>).</item>
///   <item><c>Values[1]</c>: the current favourites list (<see cref="IReadOnlyList{ToolDefinition}"/>),
///         re-evaluated whenever <c>HomeViewModel.FavoriteTools</c> raises INPC.</item>
/// </list>
/// </para>
/// </summary>
internal sealed class FavoriteCheckConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not string key
            || values[1] is not IReadOnlyList<ToolDefinition> favorites)
        {
            return false;
        }

        foreach (var tool in favorites)
        {
            if (tool.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
