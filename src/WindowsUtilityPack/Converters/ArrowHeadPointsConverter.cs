using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Computes a simple arrow head polygon at the end of a line from (0,0) to (width,height).
/// </summary>
public sealed class ArrowHeadPointsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not double width
            || values[1] is not double height)
        {
            return new PointCollection();
        }

        var length = Math.Sqrt(width * width + height * height);
        if (length < 1)
        {
            return new PointCollection();
        }

        // Unit direction from start to end.
        var ux = width / length;
        var uy = height / length;

        // Perpendicular.
        var px = -uy;
        var py = ux;

        const double headLength = 14;
        const double headWidth = 8;

        var end = new Point(width, height);
        var basePoint = new Point(
            width - ux * headLength,
            height - uy * headLength);

        var left = new Point(
            basePoint.X + px * headWidth,
            basePoint.Y + py * headWidth);

        var right = new Point(
            basePoint.X - px * headWidth,
            basePoint.Y - py * headWidth);

        return new PointCollection { end, left, right };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

