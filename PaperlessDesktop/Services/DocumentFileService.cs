using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using PaperlessDesktop.Models;

namespace PaperlessDesktop.Services;

public class DocumentFileService
{
    private readonly PaperlessApiClient _api;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PaperlessDesktop", "previews");

    public DocumentFileService(PaperlessApiClient api)
    {
        _api = api;
    }

    public async Task<string> GetPreviewFileAsync(Document document, CancellationToken ct)
    {
        Directory.CreateDirectory(_folder);

        string baseName = $"{document.Id}_{document.Modified?.Ticks ?? 0}";

        string? cached = Directory.EnumerateFiles(_folder, baseName + ".*").FirstOrDefault();
        if (cached is not null)
            return cached;

        var (data, contentType) = await _api.GetPreviewAsync(document.Id, ct);

        string finalPath = Path.Combine(_folder, baseName + ExtensionFor(contentType));
        string partialPath = Path.Combine(_folder, $"tmp_{Guid.NewGuid():N}.part");
        await File.WriteAllBytesAsync(partialPath, data, ct);
        File.Move(partialPath, finalPath, overwrite: true);

        return finalPath;
    }

    public bool TryOpenWithDefaultApp(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void CleanUp()
    {
        try
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ExtensionFor(string? contentType) => contentType switch
    {
        "application/pdf" => ".pdf",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        "image/tiff" => ".tiff",
        "text/plain" => ".txt",
        _ => ".pdf"
    };
}