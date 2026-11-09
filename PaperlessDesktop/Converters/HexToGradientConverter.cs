using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.Converters;

/// <summary>Hex accent color -> the sidebar gradient it produces (for the color swatches in Settings).</summary>
public class HexToGradientConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string;
        if (!ThemeService.IsValidColor(hex))
            return Brushes.Gray;
        return ThemeService.CreateSidebarGradient(ThemeService.ParseColor(hex, ThemeService.DefaultPrimary));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
