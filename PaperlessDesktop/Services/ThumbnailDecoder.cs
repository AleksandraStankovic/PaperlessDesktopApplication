using System.IO;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace PaperlessDesktop.Services;

/// <summary>
/// Turns image bytes (WebP, PNG, JPEG) into a WPF image.
/// Paperless thumbnails are WebP, which WPF cannot always decode on its own.
/// </summary>
public static class ThumbnailDecoder
{
    public static BitmapSource? Decode(byte[] data, int maxWidth = 300)
    {
        // 1. Decode with SkiaSharp (understands WebP)
        using var skBitmap = SKBitmap.Decode(data);
        if (skBitmap is null)
            return null;

        // 2. Re-encode as PNG, which WPF always understands
        using var skImage = SKImage.FromBitmap(skBitmap);
        using var png = skImage.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(png.ToArray());

        // 3. Load the PNG into a WPF image
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;   // read everything now, so the stream can be closed
        bitmap.DecodePixelWidth = maxWidth;             // don't keep a huge image in memory
        bitmap.StreamSource = stream;
        bitmap.EndInit();

        bitmap.Freeze();   // makes it safe to create on a background thread and show on the UI thread
        return bitmap;
    }
}