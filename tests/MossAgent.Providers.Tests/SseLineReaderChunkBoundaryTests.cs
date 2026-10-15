using System.Text;
using MossAgent.Providers.Streaming;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class SseLineReaderChunkBoundaryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(8192)]
    public async Task MixedEndingsAndFragmentedUtf8_PreserveLines(int chunkSize)
    {
        const string payload = "\uFEFFfirst\r\n\nsecond\rthird\r\r\nfourth\n尾部🔎\uFEFF";
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(payload), chunkSize);

        Assert.Equal(["first", "", "second", "third", "", "fourth", "尾部🔎\uFEFF"],
            await CollectAsync(stream));
        Assert.False(stream.IsDisposed);
    }

    [Theory]
    [InlineData(4094)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public async Task CrLfAcrossBufferBoundaries_DoesNotEmitAnExtraLine(int length)
    {
        var value = new string('x', length);
        var payload = $"{value}\r\nsmall\n\r\n{value}\ntail\r";
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(payload));

        Assert.Equal([value, "small", "", value, "tail"], await CollectAsync(stream));
    }

    [Theory]
    [InlineData("", new string[] { })]
    [InlineData("\uFEFF", new string[] { })]
    [InlineData("\uFEFF\r\n", new string[] { "" })]
    [InlineData("\uFEFF\r\n\n", new string[] { "", "" })]
    [InlineData("\uFEFF\uFEFF\n", new string[] { "\uFEFF" })]
    public async Task EmptyInputAndBomOnlyLines_KeepExistingEofBehavior(string payload, string[] expected)
    {
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(payload), 1);

        Assert.Equal(expected, await CollectAsync(stream));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(0, "\n")]
    [InlineData(0, "\r\n")]
    [InlineData(1, "")]
    [InlineData(1, "\n")]
    [InlineData(1, "\r\n")]
    public async Task LineAtOrBelowLimit_IsAcceptedBeforeTerminatorOrEof(int belowLimit, string ending)
    {
        var value = new string('x', SseLineReader.MaximumLineCharacters - belowLimit);
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(value + ending));

        Assert.Equal(value, Assert.Single(await CollectAsync(stream)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Utf16LineLimit_CountsBothCharactersOfASupplementaryScalar(int excessScalars)
    {
        var value = string.Concat(Enumerable.Repeat("🔎",
            SseLineReader.MaximumLineCharacters / 2 + excessScalars));
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(value + "\n"), 4095);

        if (excessScalars == 0)
        {
            Assert.Equal(value, Assert.Single(await CollectAsync(stream)));
        }
        else
        {
            var failure = await Assert.ThrowsAsync<SseInputLimitException>(() => CollectAsync(stream));
            Assert.Equal("stream_line_limit", failure.Code);
            Assert.DoesNotContain("🔎", failure.Message);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task OversizedFinalSegment_IsRejectedWithoutReturningAPartialLine(string ending)
    {
        var value = new string('x', SseLineReader.MaximumLineCharacters + 1);
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes(value + ending));

        var failure = await Assert.ThrowsAsync<SseInputLimitException>(() => CollectAsync(stream));

        Assert.Equal("stream_line_limit", failure.Code);
        Assert.DoesNotContain(new string('x', 40), failure.Message);
    }

    [Fact]
    public async Task CompleteLines_AreDeliveredWithoutAnotherReadOrOwningTheStream()
    {
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes("first\nsecond\r\n"),
            blockAtEnd: true);
        await using var lines = SseLineReader.ReadAsync(stream, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await lines.MoveNextAsync());
        var first = lines.Current;
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("second", lines.Current);
        Assert.Equal("first", first);
        Assert.False(stream.BlockedRead.Task.IsCompleted);
        await lines.DisposeAsync();
        Assert.False(stream.IsDisposed);
    }

    [Fact]
    public async Task PreCancelledRead_DoesNotConsumeOrDisposeTheStream()
    {
        await using var stream = new ChunkedProviderReadStream(Encoding.UTF8.GetBytes("line\n"));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await using var lines = SseLineReader.ReadAsync(stream, cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lines.MoveNextAsync().AsTask());
        Assert.Equal(0, stream.BytesRead);
        Assert.False(stream.IsDisposed);
    }

    [Fact]
    public async Task InvalidUtf8AfterAnEarlierChunk_DoesNotChangePreviouslyReturnedLines()
    {
        await using var stream = new ChunkedProviderReadStream([.. Encoding.UTF8.GetBytes("good\n"), 0xC3, 0x28], 5);
        await using var lines = SseLineReader.ReadAsync(stream, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await lines.MoveNextAsync());
        var first = lines.Current;
        await Assert.ThrowsAsync<DecoderFallbackException>(() => lines.MoveNextAsync().AsTask());
        Assert.Equal("good", first);
        Assert.False(stream.IsDisposed);
    }

    private static async Task<List<string>> CollectAsync(Stream stream)
    {
        var lines = new List<string>();
        await foreach (var line in SseLineReader.ReadAsync(stream, TestContext.Current.CancellationToken))
            lines.Add(line);
        return lines;
    }
}
