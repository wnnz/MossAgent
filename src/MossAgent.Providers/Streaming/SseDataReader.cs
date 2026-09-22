using System.Runtime.CompilerServices;
using System.Text;

namespace MossAgent.Providers.Streaming;

internal static class SseDataReader
{
    public static async IAsyncEnumerable<string> ReadAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    yield return data.ToString();
                    data.Clear();
                }

                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            if (data.Length > 0)
            {
                data.Append('\n');
            }

            data.Append(line.AsSpan(5).TrimStart());
        }

        if (data.Length > 0)
        {
            yield return data.ToString();
        }
    }
}

