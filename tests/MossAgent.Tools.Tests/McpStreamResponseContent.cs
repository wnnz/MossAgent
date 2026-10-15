using System.Net;
using System.Net.Http.Headers;

namespace MossAgent.Tools.Tests;

internal sealed class McpStreamResponseContent : HttpContent
{
    private readonly Stream _stream;
    private readonly long? _length;

    public McpStreamResponseContent(Stream stream, string mediaType, long? length = null)
    {
        _stream = stream;
        _length = length;
        Headers.ContentType = new MediaTypeHeaderValue(mediaType);
    }

    protected override bool TryComputeLength(out long computedLength)
    {
        computedLength = _length ?? 0;
        return _length.HasValue;
    }

    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(_stream);
    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => Task.FromResult(_stream);
    protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) => _stream.CopyToAsync(target);
    protected override Task SerializeToStreamAsync(Stream target, TransportContext? context, CancellationToken cancellationToken) =>
        _stream.CopyToAsync(target, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _stream.Dispose();
        base.Dispose(disposing);
    }
}
