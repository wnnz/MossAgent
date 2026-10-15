namespace MossAgent.Providers.Tests;

internal sealed class ChunkedProviderReadStream(byte[] content, int chunkSize = 4096,
    bool blockAtEnd = false) : Stream
{
    public int BytesRead { get; private set; }
    public bool IsDisposed { get; private set; }
    public TaskCompletionSource BlockedRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BytesRead >= content.Length && blockAtEnd)
        {
            BlockedRead.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        var count = Math.Min(Math.Min(buffer.Length, chunkSize), content.Length - BytesRead);
        content.AsMemory(BytesRead, count).CopyTo(buffer);
        BytesRead += count;
        return count;
    }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
