namespace MossAgent.Tools.Tests;

internal sealed class McpHttpResponseStream(byte[] bytes, bool keepOpen = false) : Stream
{
    private readonly MemoryStream _content = new(bytes, writable: false);
    public long BytesRead { get; private set; }
    public bool IsDisposed { get; private set; }
    public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _content.Read(buffer, offset, count);
        BytesRead += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReadStarted.TrySetResult();
        var read = await _content.ReadAsync(buffer, cancellationToken);
        BytesRead += read;
        if (read == 0 && keepOpen && buffer.Length > 0)
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        if (disposing) _content.Dispose();
        base.Dispose(disposing);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
