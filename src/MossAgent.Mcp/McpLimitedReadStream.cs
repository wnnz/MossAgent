namespace MossAgent.Mcp;

internal sealed class McpLimitedReadStream(Stream source, int maximumBytes) : Stream
{
    private int _bytesRead;

    public override bool CanRead => source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var count = source.Read(buffer[..AllowedCount(buffer.Length)]);
        AccountForRead(count);
        return count;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = await source.ReadAsync(buffer[..AllowedCount(buffer.Length)], cancellationToken).ConfigureAwait(false);
        AccountForRead(count);
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private int AllowedCount(int requested) => Math.Min(requested, maximumBytes - _bytesRead + 1);

    private void AccountForRead(int count)
    {
        _bytesRead += count;
        if (_bytesRead > maximumBytes) throw new IOException("MCP HTTP 响应超过 2 MiB 大小限制。");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) source.Dispose();
        base.Dispose(disposing);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
