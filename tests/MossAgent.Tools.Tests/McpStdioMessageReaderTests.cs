using MossAgent.Mcp;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpStdioMessageReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BufferedMessages_PreserveLinesUnicodeCrLfAndFinalUnterminatedFrame()
    {
        var reader = new McpStdioMessageReader(new StringReader("first\r\n\n中文 🚀\nlast"), 32);

        Assert.Equal("first", await reader.ReadLineAsync(Token));
        Assert.Equal(string.Empty, await reader.ReadLineAsync(Token));
        Assert.Equal("中文 🚀", await reader.ReadLineAsync(Token));
        Assert.Equal("last", await reader.ReadLineAsync(Token));
        Assert.Null(await reader.ReadLineAsync(Token));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(4096)]
    [InlineData(8192)]
    public async Task ExactLimit_IsAcceptedAcrossBufferBoundaries(int limit)
    {
        var content = new string('a', limit);
        var reader = new McpStdioMessageReader(new StringReader(content + "\nnext\n"), limit);

        Assert.Equal(content, await reader.ReadLineAsync(Token));
        Assert.Equal("next", await reader.ReadLineAsync(Token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OversizedFrames_AreRejectedEvenWithoutANewline(bool terminated)
    {
        var reader = new McpStdioMessageReader(new StringReader(new string('x', 8193)
            + (terminated ? "\n" : string.Empty)), 8192);

        await Assert.ThrowsAsync<IOException>(() => reader.ReadLineAsync(Token));
    }

    [Fact]
    public async Task CancelledRead_DoesNotDeliverAlreadyBufferedFrames()
    {
        var reader = new McpStdioMessageReader(new StringReader("first\nsecond\n"));
        Assert.Equal("first", await reader.ReadLineAsync(Token));
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadLineAsync(cancelled.Token));
    }
}
