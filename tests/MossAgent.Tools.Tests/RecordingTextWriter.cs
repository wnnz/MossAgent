using System.Text;

namespace MossAgent.Tools.Tests;

internal sealed class RecordingTextWriter : TextWriter
{
    private readonly StringBuilder _buffer = new();
    public bool Disposed { get; private set; }
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) => _buffer.Append(value);

    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _buffer.Append(buffer);
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }

    public override string ToString() => _buffer.ToString();
}
