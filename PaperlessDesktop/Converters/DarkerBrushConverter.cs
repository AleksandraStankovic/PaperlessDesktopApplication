using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PaperlessDesktop.Converters;

public class DarkerBrushConverter : IValueConverter
{
    public double Factor { get; set; } = 0.75;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SolidColorBrush brush)
            return Brushes.Transparent;

        Color c = brush.Color;
        var darker = Color.FromArgb(c.A, Scale(c.R), Scale(c.G), Scale(c.B));
        var result = new SolidColorBrush(darker);
        result.Freeze();
        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private byte Scale(byte channel) => (byte)Math.Clamp(Math.Round(channel * Factor), 0, 255);
}
