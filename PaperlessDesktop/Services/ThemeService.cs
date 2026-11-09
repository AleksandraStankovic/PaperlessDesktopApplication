using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace PaperlessDesktop.Services;

/// <summary>Switches light/dark theme and the accent (primary) color at runtime.</summary>
public class ThemeService
{
    public static readonly Color DefaultPrimary = Color.FromRgb(0x25, 0x63, 0xEB);   // Bright blue, white text 5.2:1 (WCAG AA)

    private readonly PaletteHelper _palette = new();

    public void Apply(bool isDark, Color primaryColor)
    {
        Theme theme = _palette.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
        theme.SetPrimaryColor(primaryColor);
        _palette.SetTheme(theme);
    }

    public static bool IsValidColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        try
        {
            ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static Color ParseColor(string? hex, Color fallback)
        => IsValidColor(hex) ? (Color)ColorConverter.ConvertFromString(hex!) : fallback;

    public static string ToHex(Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";
}