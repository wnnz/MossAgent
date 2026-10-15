using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpHttpResponseSafetyTests
{
    [Theory]
    [InlineData("application/json", true)]
    [InlineData("application/json", false)]
    [InlineData("text/event-stream", true)]
    [InlineData("text/event-stream", false)]
    public async Task OversizedResponses_AreRejectedWithBoundedReads(string mediaType, bool declaredLength)
    {
        var handler = new McpHttpTestHandler();
        McpHttpResponseStream? stream = null;
        handler.Respond = (message, _) =>
        {
            if (message.GetProperty("method").GetString() != "tools/call")
                return Task.FromResult(McpHttpTestHandler.DefaultResponse(message));
            var json = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = message.GetProperty("id").GetInt64(),
                result = new { content = new[] { new { type = "text", text = new string('中', 1024 * 1024) } } }
            });
            var bytes = Encoding.UTF8.GetBytes(mediaType == "text/event-stream" ? $"data: {json}\n\n" : json);
            stream = new McpHttpResponseStream(bytes);
            var content = new McpStreamResponseContent(stream, mediaType, declaredLength ? bytes.Length : null);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<IOException>(() => client.CallToolAsync("echo", JsonSerializer.SerializeToElement(new { }),
            TestContext.Current.CancellationToken));
        Assert.NotNull(stream);
        Assert.InRange(stream.BytesRead, 0, 2 * 1024 * 1024 + 1);
        if (declaredLength) Assert.Equal(0, stream.BytesRead);
        Assert.True(stream.IsDisposed);
    }

    [Theory]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":9999,\"result\":{}}")]
    [InlineData("{\"jsonrpc\":\"1.0\",\"id\":2,\"result\":{}}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{},\"error\":{}}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":2}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":\"2\",\"result\":{}}")]
    [InlineData("[\"synthetic-secret\"]")]
    [InlineData("synthetic-secret:invalid-json")]
    public async Task InvalidProtocolResponses_FailWithoutEchoingTheirBody(string payload)
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (message, _) => Task.FromResult(message.GetProperty("method").GetString() == "tools/call"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") }
                : McpHttpTestHandler.DefaultResponse(message))
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAnyAsync<IOException>(() => client.CallToolAsync("echo",
            JsonSerializer.SerializeToElement(new { }), TestContext.Current.CancellationToken));
        Assert.DoesNotContain("synthetic-secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RpcErrors_DoNotEchoSyntheticSecrets()
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (message, _) => Task.FromResult(message.GetProperty("method").GetString() == "tools/call"
                ? McpHttpTestHandler.JsonResponse(new
                {
                    jsonrpc = "2.0",
                    id = message.GetProperty("id").GetInt64(),
                    error = new { code = -32000, message = "Authorization: Bearer synthetic-secret" }
                })
                : McpHttpTestHandler.DefaultResponse(message))
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAnyAsync<IOException>(() => client.CallToolAsync("echo",
            JsonSerializer.SerializeToElement(new { }), TestContext.Current.CancellationToken));
        Assert.DoesNotContain("synthetic-secret", error.ToString(), StringComparison.Ordinal);
    }
}
