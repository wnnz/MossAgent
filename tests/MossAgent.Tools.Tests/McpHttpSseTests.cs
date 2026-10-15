using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpHttpSseTests
{
    [Fact]
    public async Task MatchingMultilineResponse_CompletesWithoutWaitingForStreamClosure()
    {
        var handler = new McpHttpTestHandler();
        McpHttpResponseStream? stream = null;
        handler.Respond = (message, _) =>
        {
            if (message.GetProperty("method").GetString() != "tools/call")
                return Task.FromResult(McpHttpTestHandler.DefaultResponse(message));
            var id = message.GetProperty("id").GetInt64();
            var payload = ": heartbeat\r\n\r\n"
                + "data: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}\r\n\r\n"
                + "data: {\"jsonrpc\":\"2.0\",\"id\":9999,\"result\":{}}\r\n\r\n"
                + "event: message\r\nid: event-1\r\ndata: {\"jsonrpc\":\"2.0\",\r\n"
                + $"data: \"id\":{id},\"result\":{{\"content\":[{{\"type\":\"text\",\"text\":\"中文 🙂\"}}]}}}}\r\n\r\n";
            stream = new McpHttpResponseStream(Encoding.UTF8.GetBytes(payload), keepOpen: true);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new McpStreamResponseContent(stream, "text/event-stream")
            });
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        var result = await client.CallToolAsync("echo", JsonSerializer.SerializeToElement(new { }), timeout.Token);

        Assert.Equal("中文 🙂", result.Content);
        Assert.NotNull(stream);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public async Task ProgressAndHeartbeatBytes_AreIncludedInTheResponseBudget()
    {
        var handler = new McpHttpTestHandler();
        McpHttpResponseStream? stream = null;
        handler.Respond = (message, _) =>
        {
            if (message.GetProperty("method").GetString() != "tools/call")
                return Task.FromResult(McpHttpTestHandler.DefaultResponse(message));
            var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(": heartbeat\n\n", 200_000)));
            stream = new McpHttpResponseStream(bytes);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new McpStreamResponseContent(stream, "text/event-stream")
            });
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<IOException>(() => client.CallToolAsync("echo", JsonSerializer.SerializeToElement(new { }),
            TestContext.Current.CancellationToken));
        Assert.NotNull(stream);
        Assert.InRange(stream.BytesRead, 0, 2 * 1024 * 1024 + 1);
    }

    [Fact]
    public async Task StreamWithoutMatchingResult_IsRejected()
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (message, _) => Task.FromResult(message.GetProperty("method").GetString() == "tools/call"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "data: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\"}\n\n",
                        Encoding.UTF8, "text/event-stream")
                }
                : McpHttpTestHandler.DefaultResponse(message))
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<IOException>(() => client.CallToolAsync("echo", JsonSerializer.SerializeToElement(new { }),
            TestContext.Current.CancellationToken));
    }
}
