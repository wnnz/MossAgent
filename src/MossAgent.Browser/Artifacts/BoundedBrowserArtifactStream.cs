using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Browser.Artifacts;

internal sealed class BoundedBrowserArtifactStream(Stream destination) : Stream
{
    private long _written;
    private bool _disposed;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_disposed && destination.CanWrite;
    public override long Length => _written;
    public override long Position { get => _written; set => throw new NotSupportedException(); }
    public override void Flush() { ObjectDisposedException.ThrowIf(_disposed, this); destination.Flush(); }
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return destination.FlushAsync(cancellationToken);
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Validate(buffer.Length);
        destination.Write(buffer);
        _written += buffer.Length;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(buffer.Length);
        await destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _written += buffer.Length;
    }

    private void Validate(int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (length > BrowserArtifactLimits.MaximumBytes - _written)
            throw new FileMutationSessionException(BrowserToolText.Get("ArtifactTooLarge"), "browser_artifact_too_large");
    }

    protected override void Dispose(bool disposing) { _disposed = true; base.Dispose(disposing); }
}
