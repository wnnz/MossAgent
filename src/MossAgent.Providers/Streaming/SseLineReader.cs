using System.Runtime.CompilerServices;
using System.Text;

namespace MossAgent.Providers.Streaming;

internal static class SseLineReader
{
    public const int MaximumLineCharacters = 1024 * 1024;

    public static async IAsyncEnumerable<string> ReadAsync(
        Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var encoding = new UTF8Encoding(false, true);
        var decoder = encoding.GetDecoder();
        var bytes = new byte[4096];
        var buffer = new char[encoding.GetMaxCharCount(bytes.Length)];
        StringBuilder? line = null;
        var skipLineFeed = false;
        var atStart = true;
        while (true)
        {
            var bytesRead = await stream.ReadAsync(bytes.AsMemory(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var count = decoder.GetChars(bytes.AsSpan(0, bytesRead), buffer.AsSpan(), flush: bytesRead == 0);
            if (bytesRead == 0) break;
            if (count == 0) continue;
            var offset = 0;
            if (atStart)
            {
                atStart = false;
                if (buffer[0] == '\uFEFF') offset++;
            }
            while (offset < count)
            {
                if (skipLineFeed)
                {
                    skipLineFeed = false;
                    if (buffer[offset] == '\n')
                    {
                        offset++;
                        continue;
                    }
                }
                var terminatorOffset = buffer.AsSpan(offset, count - offset).IndexOfAny('\r', '\n');
                var length = terminatorOffset < 0 ? count - offset : terminatorOffset;
                if ((line?.Length ?? 0) > MaximumLineCharacters - length)
                    throw new SseInputLimitException("stream_line_limit", "StreamLineLimit");
                if (terminatorOffset < 0)
                {
                    (line ??= new StringBuilder()).Append(buffer, offset, length);
                    break;
                }
                var value = line is { Length: > 0 }
                    ? line.Append(buffer, offset, length).ToString()
                    : new string(buffer, offset, length);
                line?.Clear();
                offset += length;
                skipLineFeed = buffer[offset++] == '\r';
                yield return value;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (line is { Length: > 0 }) yield return line.ToString();
    }
}
