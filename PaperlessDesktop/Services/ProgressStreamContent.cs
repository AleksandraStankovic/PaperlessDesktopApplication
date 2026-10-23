using System.IO;
using System.Net;
using System.Net.Http;

namespace PaperlessDesktop.Services;

/// <summary>
/// Sends a file in chunks and reports how much has been sent (0.0 - 1.0),
/// so the UI can show a real progress bar.
/// </summary>
internal sealed class ProgressStreamContent : HttpContent
{
    private const int BufferSize = 81920;   // 80 KB per chunk

    private readonly Stream _source;
    private readonly IProgress<double>? _progress;

    public ProgressStreamContent(Stream source, IProgress<double>? progress)
    {
        _source = source;
        _progress = progress;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context,
                                                         CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        long total = _source.Length;
        long sent = 0;
        int read;

        while ((read = await _source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sent += read;
            _progress?.Report(total == 0 ? 1 : (double)sent / total);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _source.Length;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _source.Dispose();
        base.Dispose(disposing);
    }
}