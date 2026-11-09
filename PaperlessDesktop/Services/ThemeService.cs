using System.Windows;
using System.Windows.Media;
using MaterialDesignColors.ColorManipulation;
using MaterialDesignThemes.Wpf;

namespace PaperlessDesktop.Services;

/// <summary>Switches light/dark theme and the accent (primary) color at runtime.</summary>
public class ThemeService
{
    // "Sunset": violet at the top of the sidebar, fading to raspberry at the bottom
    public static readonly Color DefaultPrimary = Color.FromRgb(0x6D, 0x28, 0xD9);   // Violet, white text 7.1:1 (WCAG AA)

    /// <summary>Resource keys used by the sidebar in MainWindow.xaml.</summary>
    public const string SidebarBackgroundKey = "Sidebar.Background";
    public const string SidebarSelectedBackgroundKey = "Sidebar.SelectedBackground";

    // WCAG AA for normal text is 4.5:1 - keep a small margin above it
    private const float MinContrast = 4.6f;
    // Hue the gradient moves toward (raspberry), and how far it may move
    private const double GradientTargetHue = 335;
    private const double MaxHueShift = 72;
    private const double MinGradientSaturation = 0.55;
    private const double MinGradientLightness = 0.42;

    private readonly PaletteHelper _palette = new();

    public void Apply(bool isDark, Color primaryColor)
    {
        Theme theme = _palette.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
        theme.SetPrimaryColor(primaryColor);
        _palette.SetTheme(theme);

        ApplySidebar(primaryColor);
    }

    /// <summary>
    /// Sidebar = vertical gradient from the accent color to a "sunset" partner color.
    /// MaterialDesign shows black or white text on the accent (whichever contrasts more);
    /// the partner color is adjusted until that same text still meets WCAG AA on it,
    /// so the menu stays readable for ANY color the user picks.
    /// </summary>
    private static void ApplySidebar(Color primary)
    {
        Color foreground = primary.ContrastingForegroundColor();
        LinearGradientBrush gradient = CreateSidebarGradient(primary);

        // Selected page: darken under white text, lighten under black text (contrast only goes up)
        var selected = new SolidColorBrush(foreground == Colors.White
            ? Color.FromArgb(0x40, 0, 0, 0)
            : Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF));
        selected.Freeze();

        var resources = Application.Current.Resources;
        resources[SidebarBackgroundKey] = gradient;
        resources[SidebarSelectedBackgroundKey] = selected;
    }

    /// <summary>The sidebar gradient for an accent color (also used for the color swatches in Settings).</summary>
    public static LinearGradientBrush CreateSidebarGradient(Color primary)
    {
        Color end = GradientEnd(primary, primary.ContrastingForegroundColor());
        var gradient = new LinearGradientBrush(primary, end, new Point(0, 0), new Point(0, 1));
        gradient.Freeze();
        return gradient;
    }

    private static Color GradientEnd(Color primary, Color foreground)
    {
        ToHsl(primary, out double h, out double s, out double l);

        // Move the hue toward raspberry (violet -> raspberry, orange -> pink, blue -> purple...).
        // Colors already near raspberry go the other way (toward violet); colors on the
        // opposite side of the wheel go toward blue, which looks better than yellow/olive.
        double diff = ((GradientTargetHue - h + 540) % 360) - 180;
        double shift = Math.Abs(diff) < 30 ? -60
                     : Math.Abs(diff) > 120 ? MaxHueShift
                     : Math.Clamp(diff, -MaxHueShift, MaxHueShift);
        bool darken = foreground == Colors.White;

        // Light accents (black text: orange, yellow...) always take the full shift,
        // otherwise their gradient looks too flat
        if (!darken)
            shift = Math.Sign(shift) * MaxHueShift;

        h = (h + shift + 360) % 360;

        // Muted/dark colors (brown, blue grey) barely change with a hue shift alone,
        // so give the partner color some saturation and lightness to make the gradient visible.
        // The contrast loop below still brings it back to a readable shade.
        s = Math.Max(s, MinGradientSaturation);
        if (darken)
            l = Math.Max(l, MinGradientLightness);

        Color end = FromHsl(h, s, l);
        while (end.ContrastRatio(foreground) < MinContrast && l > 0 && l < 1)
        {
            l = darken ? Math.Max(0, l - 0.01) : Math.Min(1, l + 0.01);
            end = FromHsl(h, s, l);
        }
        return end;
    }

    private static void ToHsl(Color c, out double h, out double s, out double l)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2;
        double d = max - min;
        if (d == 0)
        {
            h = s = 0;
            return;
        }
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        if (max == r)
            h = ((g - b) / d + (g < b ? 6 : 0)) * 60;
        else if (max == g)
            h = ((b - r) / d + 2) * 60;
        else
            h = ((r - g) / d + 4) * 60;
    }

    private static Color FromHsl(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = l - c / 2;
        (double r, double g, double b) = (h / 60) switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        static byte B(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return Color.FromRgb(B(r + m), B(g + m), B(b + m));
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