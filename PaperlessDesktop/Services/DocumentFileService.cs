using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using PaperlessDesktop.Models;

namespace PaperlessDesktop.Services;

/// <summary>Downloads document files to a temp folder (for preview) and opens them externally.</summary>
public class DocumentFileService
{
    private readonly PaperlessApiClient _api;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PaperlessDesktop", "previews");

    public DocumentFileService(PaperlessApiClient api)
    {
        _api = api;
    }

    /// <summary>Returns a local file path for the document's preview, downloading it if needed.</summary>
    public async Task<string> GetPreviewFileAsync(Document document, CancellationToken ct)
    {
        Directory.CreateDirectory(_folder);

        // File name includes the "modified" time: if the document changes on the server, we download it again
        string baseName = $"{document.Id}_{document.Modified?.Ticks ?? 0}";

        string? cached = Directory.EnumerateFiles(_folder, baseName + ".*").FirstOrDefault();
        if (cached is not null)
            return cached;

        var (data, contentType) = await _api.GetPreviewAsync(document.Id, ct);

        // Write to a temporary name first, then rename: a cancelled download never leaves a broken file behind
        string finalPath = Path.Combine(_folder, baseName + ExtensionFor(contentType));
        string partialPath = Path.Combine(_folder, $"tmp_{Guid.NewGuid():N}.part");
        await File.WriteAllBytesAsync(partialPath, data, ct);
        File.Move(partialPath, finalPath, overwrite: true);

        return finalPath;
    }

    /// <summary>Opens a file in the app Windows uses for that file type (e.g. Adobe Reader, Edge).</summary>
    public bool TryOpenWithDefaultApp(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Win32Exception)
        {
            return false;   // no app is associated with this file type
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Deletes preview files from earlier sessions.</summary>
    public void CleanUp()
    {
        try
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A file is still in use - not important, we'll try again next start
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