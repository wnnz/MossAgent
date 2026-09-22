using System.Text;
using MossAgent.Providers.Streaming;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class SseDataReaderTests
{
    [Fact]
    public async Task ReadAsync_CombinesDataLinesAndIgnoresOtherFields()
    {
        const string payload = "event: message\ndata: first\ndata: second\n\n: keepalive\ndata: final";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var events = new List<string>();

        await foreach (var item in SseDataReader.ReadAsync(
            stream, TestContext.Current.CancellationToken))
        {
            events.Add(item);
        }

        Assert.Equal(["first\nsecond", "final"], events);
    }
}
