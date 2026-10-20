namespace MossAgent.Infrastructure.Tests;

internal sealed class SourceIdentityTestStream(int length, bool block = false) : Stream
{
    private int _remaining = length;
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Disposed { get; private set; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Started.TrySetResult();
        if (block) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var count = Math.Min(buffer.Length, _remaining);
        buffer.Span[..count].Fill((byte)'x');
        _remaining -= count;
        return count;
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }

    public override void Flush() => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
