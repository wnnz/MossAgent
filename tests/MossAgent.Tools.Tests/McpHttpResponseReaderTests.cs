using System.Text;
using MossAgent.Mcp;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpHttpResponseReaderTests
{
    [Theory]
    [InlineData("application/json")]
    [InlineData("text/event-stream")]
    public async Task Utf8ByteOrderMark_IsAccepted(string mediaType)
    {
        const string json = "{\"jsonrpc\":\"2.0\",\"id\":7,\"result\":{\"tools\":[]}}";
        var text = mediaType == "text/event-stream" ? $"data: {json}\n\n" : json;
        using var content = new McpStreamResponseContent(
            new MemoryStream([0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes(text)]), mediaType);

        var result = await McpHttpResponseReader.ReadAsync(content, 7, TestContext.Current.CancellationToken);

        Assert.Equal(7, result.GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task NonUtf8Bytes_AreRejectedWithoutDecoderDetails()
    {
        using var content = new McpStreamResponseContent(new MemoryStream([0xff, 0xfe, 0x11]), "application/json");

        var error = await Assert.ThrowsAsync<IOException>(() =>
            McpHttpResponseReader.ReadAsync(content, 1, TestContext.Current.CancellationToken));

        Assert.Contains("UTF-8", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task UnsupportedContentType_IsRejected()
    {
        using var content = new McpStreamResponseContent(new MemoryStream(
            Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}")), "text/html");

        await Assert.ThrowsAsync<IOException>(() =>
            McpHttpResponseReader.ReadAsync(content, 1, TestContext.Current.CancellationToken));
    }
}
