using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PaperlessDesktop.Converters;

/// <summary>
/// Makes a darker shade of a solid color (same hue, less light).
/// Used for the circle behind the ✕ on chips, so it matches the tag's own color.
/// </summary>
public class DarkerBrushConverter : IValueConverter
{
    /// <summary>How much of the original brightness is kept (0–1). Lower = darker.</summary>
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
