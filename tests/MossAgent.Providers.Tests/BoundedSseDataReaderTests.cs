using System.Text;
using MossAgent.Providers.Streaming;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class BoundedSseDataReaderTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public async Task FragmentedUtf8BomAndAllLineEndings_PreserveData(string ending)
    {
        var payload = $"\uFEFFdata: 中文🔎{ending}data:  保留空格{ending}{ending}data{ending}{ending}";
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(payload), 1);

        var events = await CollectAsync(stream);

        Assert.Equal(["中文🔎\n 保留空格", ""], events);
        Assert.False(stream.IsDisposed);
    }

    [Theory]
    [InlineData("data:")]
    [InlineData(":")]
    [InlineData("ignored:")]
    public async Task OversizedLines_StopBeforeReadingTheRemainder(string prefix)
    {
        var bytes = Encoding.UTF8.GetBytes(prefix + new string('x', SseLineReader.MaximumLineCharacters * 2));
        await using var stream = new ChunkedProviderReadStream(bytes);

        var failure = await Assert.ThrowsAsync<SseInputLimitException>(() => CollectAsync(stream));

        Assert.Equal("stream_line_limit", failure.Code);
        Assert.True(stream.BytesRead <= SseLineReader.MaximumLineCharacters + 4096);
        Assert.True(stream.BytesRead < bytes.Length);
        Assert.DoesNotContain(new string('x', 40), failure.Message);
    }

    [Fact]
    public async Task ExactLineAndEventLimits_AreAccepted()
    {
        var first = "data:" + new string('a', SseLineReader.MaximumLineCharacters - 5);
        var second = "data:" + new string('b', SseLineReader.MaximumLineCharacters - 5);
        var payload = first + "\n" + second + "\ndata:" + new string('c', 8) + "\n\n";
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(payload));

        var events = await CollectAsync(stream);

        Assert.Equal(SseDataReader.MaximumEventCharacters, Assert.Single(events).Length);
    }

    [Fact]
    public async Task EventLimit_IncludesEmptyDataSeparatorsAndCannotBeBypassedWithSmallLines()
    {
        var line = "data:" + new string('x', 1023) + "\n";
        var payload = string.Concat(Enumerable.Repeat(line, SseDataReader.MaximumEventCharacters / 1024 + 2));
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(payload));

        var failure = await Assert.ThrowsAsync<SseInputLimitException>(() => CollectAsync(stream));

        Assert.Equal("stream_event_limit", failure.Code);
    }

    [Fact]
    public async Task EmptyDataLines_CountTowardTheEventLimit()
    {
        var line = "data:" + new string('x', SseLineReader.MaximumLineCharacters - 5) + "\n";
        var payload = line + line + "data:\ndata:\ndata:\ndata:\ndata:\ndata:\ndata:\ndata:\ndata:\ndata:\n\n";
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(payload));

        var failure = await Assert.ThrowsAsync<SseInputLimitException>(() => CollectAsync(stream));

        Assert.Equal("stream_event_limit", failure.Code);
    }

    [Theory]
    [InlineData(new byte[] { 0xC3, 0x28 })]
    [InlineData(new byte[] { 0xF0, 0x9F, 0x94 })]
    [InlineData(new byte[] { 0xFF, 0xFE })]
    public async Task InvalidOrTruncatedUtf8_IsRejected(byte[] suffix)
    {
        await using var stream = new ChunkedProviderReadStream([.. Encoding.UTF8.GetBytes("data: "), .. suffix], 1);

        await Assert.ThrowsAsync<DecoderFallbackException>(() => CollectAsync(stream));
    }

    [Fact]
    public async Task CompleteEvent_IsDeliveredWithoutWaitingForAnotherChunkOrEof()
    {
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes("data: immediate\n\n"), blockAtEnd: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var events = SseDataReader.ReadAsync(stream, cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        Assert.True(await events.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token));
        Assert.Equal("immediate", events.Current);
        Assert.False(stream.BlockedRead.Task.IsCompleted);
    }

    [Fact]
    public async Task CancellationDuringPendingRead_PropagatesWithoutTakingOwnershipOfTheStream()
    {
        await using var stream = new ChunkedProviderReadStream([], blockAtEnd: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var events = ProviderSseReader.ReadAsync(stream, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var pending = events.MoveNextAsync().AsTask();
        await stream.BlockedRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(stream.IsDisposed);
    }

    [Fact]
    public async Task EachEventHasItsOwnBudget_AndIncompleteFinalEventKeepsExistingEofBehavior()
    {
        var value = new string('x', SseLineReader.MaximumLineCharacters - 5);
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes($"data:{value}\n\ndata:{value}"));

        Assert.Equal([value, value], await CollectAsync(stream));
    }

    private static async Task<List<string>> CollectAsync(Stream stream)
    {
        var events = new List<string>();
        await foreach (var item in SseDataReader.ReadAsync(stream, TestContext.Current.CancellationToken)) events.Add(item);
        return events;
    }
}
