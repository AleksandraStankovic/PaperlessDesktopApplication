using System.Windows;
using System.Windows.Media;

namespace PaperlessDesktop.Helpers;

public static class PieGeometry
{
    public static Geometry CreateSlice(double startAngle, double sweepAngle, double radius, double innerRadius)
    {
        var center = new Point(radius, radius);

        if (sweepAngle >= 359.99)
        {
            var ring = new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(center, radius, radius),
                new EllipseGeometry(center, innerRadius, innerRadius));
            ring.Freeze();
            return ring;
        }

        Point PointAt(double angle, double r)
        {
            double radians = (angle - 90) * Math.PI / 180;
            return new Point(center.X + r * Math.Cos(radians), center.Y + r * Math.Sin(radians));
        }

        double endAngle = startAngle + sweepAngle;
        bool isLargeArc = sweepAngle > 180;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(PointAt(startAngle, radius), isFilled: true, isClosed: true);
            context.ArcTo(PointAt(endAngle, radius), new Size(radius, radius), 0,
                          isLargeArc, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
            context.LineTo(PointAt(endAngle, innerRadius), isStroked: true, isSmoothJoin: false);
            context.ArcTo(PointAt(startAngle, innerRadius), new Size(innerRadius, innerRadius), 0,
                          isLargeArc, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        return geometry;
    }
}