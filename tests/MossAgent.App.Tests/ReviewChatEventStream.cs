using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace MossAgent.App.Tests;

internal sealed class ReviewChatEventStream : Stream
{
    private readonly Channel<byte[]> _frames = Channel.CreateBounded<byte[]>(4);
    private ReadOnlyMemory<byte> _remaining;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public void Publish(string text)
    {
        var frame = Encoding.UTF8.GetBytes("data: " + JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = text }) + "\n\n");
        if (frame.Length > 4096 || !_frames.Writer.TryWrite(frame)) throw new InvalidOperationException("Review fixture frame limit.");
    }

    public void Complete() => _frames.Writer.TryComplete();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.Length == 0) return 0;
        while (_remaining.IsEmpty)
        {
            if (!await _frames.Reader.WaitToReadAsync(cancellationToken)) return 0;
            if (_frames.Reader.TryRead(out var frame)) _remaining = frame;
        }
        var count = Math.Min(buffer.Length, _remaining.Length);
        _remaining[..count].CopyTo(buffer);
        _remaining = _remaining[count..];
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) Complete(); base.Dispose(disposing); }
}
