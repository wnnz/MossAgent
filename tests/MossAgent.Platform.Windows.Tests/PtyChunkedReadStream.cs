namespace MossAgent.Platform.Windows.Tests;

internal sealed class PtyChunkedReadStream(byte[] content, int chunkSize) : MemoryStream(content)
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
}
