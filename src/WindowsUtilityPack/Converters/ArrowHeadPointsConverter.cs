using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WindowsUtilityPack.Converters;

/// <summary>
/// Computes a simple arrow head polygon at the end of a line.
/// </summary>
public sealed class ArrowHeadPointsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        // Supported bindings:
        // 1) (width,height) where the line is from (0,0) -> (width,height)
        // 2) (startX,startY,endX,endY) where the line is from (startX,startY) -> (endX,endY)
        double startX = 0;
        double startY = 0;
        double endX;
        double endY;

        if (values.Length >= 4
            && values[0] is double sx
            && values[1] is double sy
            && values[2] is double ex
            && values[3] is double ey)
        {
            startX = sx;
            startY = sy;
            endX = ex;
            endY = ey;
        }
        else if (values.Length >= 2
            && values[0] is double width
            && values[1] is double height)
        {
            endX = width;
            endY = height;
        }
        else
        {
            return new PointCollection();
        }

        var dx = endX - startX;
        var dy = endY - startY;

        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1)
        {
            return new PointCollection();
        }

        // Unit direction from start to end.
        var ux = dx / length;
        var uy = dy / length;

        // Perpendicular.
        var px = -uy;
        var py = ux;

        const double headLength = 14;
        const double headWidth = 8;

        var end = new Point(endX, endY);
        var basePoint = new Point(
            endX - ux * headLength,
            endY - uy * headLength);

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
