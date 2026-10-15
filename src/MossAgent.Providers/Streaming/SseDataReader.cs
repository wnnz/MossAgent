using System.Runtime.CompilerServices;
using System.Text;

namespace MossAgent.Providers.Streaming;

internal static class SseDataReader
{
    public const int MaximumEventCharacters = 2 * 1024 * 1024;

    public static async IAsyncEnumerable<string> ReadAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var data = new StringBuilder();
        var hasData = false;
        await foreach (var line in SseLineReader.ReadAsync(stream, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line.Length == 0)
            {
                if (hasData)
                {
                    yield return data.ToString();
                    data.Clear();
                    hasData = false;
                }

                continue;
            }

            if (line != "data" && !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var contentOffset = line.Length == 4 ? 4 : 5;
            if (line.Length > contentOffset && line[contentOffset] == ' ') contentOffset++;
            var contentLength = line.Length - contentOffset;
            var separatorLength = hasData ? 1 : 0;
            if (data.Length > MaximumEventCharacters - contentLength - separatorLength)
                throw new SseInputLimitException("stream_event_limit", "StreamEventLimit");
            if (hasData)
            {
                data.Append('\n');
            }

            data.Append(line.AsSpan(contentOffset));
            hasData = true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (hasData)
        {
            yield return data.ToString();
        }
    }
}
