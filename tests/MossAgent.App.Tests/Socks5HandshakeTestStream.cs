namespace MossAgent.App.Tests;

internal sealed class Socks5HandshakeTestStream(byte[] input) : Stream
{
    private readonly MemoryStream _input = new(input);
    public List<byte[]> Frames { get; } = [];
    public Action<ReadOnlyMemory<byte>>? Writing { get; init; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _input.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Writing?.Invoke(buffer);
        Frames.Add(buffer.ToArray());
        return ValueTask.CompletedTask;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) _input.Dispose();
        base.Dispose(disposing);
    }
}
