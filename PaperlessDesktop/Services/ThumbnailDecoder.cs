using System.IO;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace PaperlessDesktop.Services;

public static class ThumbnailDecoder
{
    public static BitmapSource? Decode(byte[] data, int maxWidth = 300)
    {
        using var skBitmap = SKBitmap.Decode(data);
        if (skBitmap is null)
            return null;

        using var skImage = SKImage.FromBitmap(skBitmap);
        using var png = skImage.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(png.ToArray());

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = maxWidth;
        bitmap.StreamSource = stream;
        bitmap.EndInit();

        bitmap.Freeze();
        return bitmap;
    }
}