using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PaperlessDesktop.Converters;

public class HexToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return TryCreateBrush(value as string)
            ?? TryCreateBrush(parameter as string)
            ?? Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush? TryCreateBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}