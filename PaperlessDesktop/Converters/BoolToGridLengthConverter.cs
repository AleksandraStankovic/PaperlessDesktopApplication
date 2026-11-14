using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PaperlessDesktop.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    public GridLength TrueLength { get; set; } = new(1, GridUnitType.Star);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueLength : new GridLength(0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}