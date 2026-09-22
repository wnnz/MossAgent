using System.Text;

namespace CodingAgent.Infrastructure.Llm;

/// <summary>逐行读取 SSE 流（data: / event: 行）。</summary>
public static class SseLineReader
{
    public static async IAsyncEnumerable<string> ReadLinesAsync(HttpResponseMessage response, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            yield return line;
        }
    }
}
