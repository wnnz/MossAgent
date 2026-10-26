namespace MossAgent.Browser.Tests;

/// <summary>Produces a fixed number of bytes without allocating them, so tests can
/// exceed the 32 MiB artifact limit without a matching MemoryStream source.</summary>
internal sealed class RepeatingReadStream(long length, byte fill = 0x5A) : Stream
{
    private long _position;
    private bool _disposed;

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) =>
        Fill(buffer.AsSpan(offset, count));
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(Fill(buffer.Span));
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { _disposed = true; base.Dispose(disposing); }

    private int Fill(Span<byte> span)
    {
        var count = (int)Math.Min(span.Length, length - _position);
        span[..count].Fill(fill);
        _position += count;
        return count;
    }
}
