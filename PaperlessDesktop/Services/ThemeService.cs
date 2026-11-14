using System.Windows;
using System.Windows.Media;
using MaterialDesignColors.ColorManipulation;
using MaterialDesignThemes.Wpf;

namespace PaperlessDesktop.Services;

public class ThemeService
{
    public static readonly Color DefaultPrimary = Color.FromRgb(0x6D, 0x28, 0xD9);

    public const string SidebarBackgroundKey = "Sidebar.Background";
    public const string SidebarSelectedBackgroundKey = "Sidebar.SelectedBackground";

    public const string AccentTextKey = "Accent.Text";
    public const string AccentSelectedOverlayKey = "Accent.SelectedOverlay";

    private static readonly (string Key, Color Light, Color Dark)[] StatusColors =
    {
        ("Status.Success.Text",       Color.FromRgb(0x2E, 0x7D, 0x32), Color.FromRgb(0x81, 0xC7, 0x84)),
        ("Status.Error.Text",         Color.FromRgb(0xC6, 0x28, 0x28), Color.FromRgb(0xEF, 0x9A, 0x9A)),
        ("Status.Warning.Text",       Color.FromRgb(0xB4, 0x53, 0x09), Color.FromRgb(0xFF, 0xB7, 0x4D)),
        ("Status.Success.Background", Color.FromRgb(0xE3, 0xF2, 0xE5), Color.FromRgb(0x1E, 0x3B, 0x24)),
        ("Status.Success.Foreground", Color.FromRgb(0x1B, 0x4D, 0x20), Color.FromRgb(0xD4, 0xED, 0xD6)),
        ("Status.Error.Background",   Color.FromRgb(0xFB, 0xE6, 0xE5), Color.FromRgb(0x4A, 0x21, 0x21)),
        ("Status.Error.Foreground",   Color.FromRgb(0x7F, 0x1D, 0x1D), Color.FromRgb(0xFA, 0xDB, 0xDA)),
    };

    private static readonly Color LightSurface = Colors.White;
    private static readonly Color DarkSurface = Color.FromRgb(0x42, 0x42, 0x42);

    private const float MinContrast = 4.6f;
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
        ApplyAccentBrushes(isDark, primaryColor);
        ApplyStatusBrushes(isDark);
    }

    private static void ApplyStatusBrushes(bool isDark)
    {
        var resources = Application.Current.Resources;
        foreach (var (key, light, dark) in StatusColors)
        {
            var brush = new SolidColorBrush(isDark ? dark : light);
            brush.Freeze();
            resources[key] = brush;
        }
    }

    private static void ApplyAccentBrushes(bool isDark, Color primary)
    {
        Color text = ReadableOn(primary, isDark ? DarkSurface : LightSurface, lighten: isDark);

        var textBrush = new SolidColorBrush(text);
        textBrush.Freeze();

        var overlay = new SolidColorBrush(Color.FromArgb(isDark ? (byte)0x66 : (byte)0x38, primary.R, primary.G, primary.B));
        overlay.Freeze();

        var resources = Application.Current.Resources;
        resources[AccentTextKey] = textBrush;
        resources[AccentSelectedOverlayKey] = overlay;
    }

    private static Color ReadableOn(Color color, Color background, bool lighten)
    {
        ToHsl(color, out double h, out double s, out double l);
        Color result = color;
        while (result.ContrastRatio(background) < MinContrast && l > 0 && l < 1)
        {
            l = lighten ? Math.Min(1, l + 0.01) : Math.Max(0, l - 0.01);
            result = FromHsl(h, s, l);
        }
        return result;
    }

    private static void ApplySidebar(Color primary)
    {
        Color foreground = primary.ContrastingForegroundColor();
        LinearGradientBrush gradient = CreateSidebarGradient(primary);

        var selected = new SolidColorBrush(foreground == Colors.White
            ? Color.FromArgb(0x40, 0, 0, 0)
            : Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF));
        selected.Freeze();

        var resources = Application.Current.Resources;
        resources[SidebarBackgroundKey] = gradient;
        resources[SidebarSelectedBackgroundKey] = selected;
    }

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

        double diff = ((GradientTargetHue - h + 540) % 360) - 180;
        double shift = Math.Abs(diff) < 30 ? -60
                     : Math.Abs(diff) > 120 ? MaxHueShift
                     : Math.Clamp(diff, -MaxHueShift, MaxHueShift);
        bool darken = foreground == Colors.White;

        if (!darken)
            shift = Math.Sign(shift) * MaxHueShift;

        h = (h + shift + 360) % 360;

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