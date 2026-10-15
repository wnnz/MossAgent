using System.Text.Json;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpHttpLifecycleSafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledCalls_SendBoundedBestEffortNotifications(bool notificationFails)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new McpHttpTestHandler
        {
            Respond = async (message, token) =>
            {
                var method = message.GetProperty("method").GetString();
                if (method == "tools/call")
                {
                    started.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                if (method == "notifications/cancelled" && notificationFails)
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return McpHttpTestHandler.DefaultResponse(message);
            }
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var call = client.CallToolAsync("echo", JsonSerializer.SerializeToElement(new { }), cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken));
        var request = Assert.Single(handler.Requests, message => message.GetProperty("method").GetString() == "tools/call");
        var notification = Assert.Single(handler.Requests, message => message.GetProperty("method").GetString() == "notifications/cancelled");
        Assert.Equal(request.GetProperty("id").GetInt64(), notification.GetProperty("params").GetProperty("requestId").GetInt64());
        Assert.True(handler.SessionHeaderObserved);
        Assert.False(notification.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task PreCancelledCalls_DoNotSendRequestsOrNotifications()
    {
        var handler = new McpHttpTestHandler();
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CallToolAsync("echo",
            JsonSerializer.SerializeToElement(new { }), cancellation.Token));
        Assert.DoesNotContain(handler.Requests, message => message.GetProperty("method").GetString()
            is "tools/call" or "notifications/cancelled");
    }

    [Fact]
    public async Task InitializationFailure_DisposesAndTerminatesTheAssignedSession()
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (message, _) =>
            {
                var response = McpHttpTestHandler.JsonResponse(new
                {
                    jsonrpc = "2.0",
                    id = message.GetProperty("id").GetInt64(),
                    error = new { code = -32000, message = "synthetic-initialize-secret" }
                });
                response.Headers.Add("Mcp-Session-Id", "fixture-session");
                return Task.FromResult(response);
            }
        };
        await using var client = handler.CreateClient();

        var error = await Assert.ThrowsAnyAsync<IOException>(() => client.InitializeAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain("synthetic-initialize-secret", error.ToString(), StringComparison.Ordinal);
        Assert.True(handler.IsDisposed);
        Assert.True(handler.DeleteObserved);
        Assert.DoesNotContain(handler.Requests, message => message.GetProperty("method").GetString() == "notifications/cancelled");
    }

    [Fact]
    public async Task OversizedRequests_AreRejectedBeforeSending()
    {
        var handler = new McpHttpTestHandler();
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<IOException>(() => client.CallToolAsync("echo",
            JsonSerializer.SerializeToElement(new { text = new string('x', 3 * 1024 * 1024) }),
            TestContext.Current.CancellationToken));
        Assert.DoesNotContain(handler.Requests, message => message.GetProperty("method").GetString() == "tools/call");
    }

    [Fact]
    public async Task CancelledInitialization_CleansUpSessionWithoutCancellationNotification()
    {
        var stream = new McpHttpResponseStream([], keepOpen: true);
        var handler = new McpHttpTestHandler
        {
            Respond = (message, _) =>
            {
                var response = McpHttpTestHandler.DefaultResponse(message);
                response.Content = new McpStreamResponseContent(stream, "application/json");
                return Task.FromResult(response);
            }
        };
        await using var client = handler.CreateClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var initialization = client.InitializeAsync(cancellation.Token);
        await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);
        Assert.True(handler.IsDisposed);
        Assert.True(handler.DeleteObserved);
        Assert.True(stream.IsDisposed);
        Assert.DoesNotContain(handler.Requests, message => message.GetProperty("method").GetString() == "notifications/cancelled");
    }

    [Fact]
    public async Task Disposal_CancelsInFlightCallsAndRejectsNewCalls()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new McpHttpTestHandler
        {
            Respond = async (message, token) =>
            {
                if (message.GetProperty("method").GetString() == "tools/call")
                {
                    started.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                return McpHttpTestHandler.DefaultResponse(message);
            }
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);
        var call = client.CallToolAsync("echo", JsonSerializer.SerializeToElement(new { }), TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        await client.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.CallToolAsync("echo",
            JsonSerializer.SerializeToElement(new { }), TestContext.Current.CancellationToken));
        Assert.True(handler.DeleteObserved);
        Assert.DoesNotContain(handler.Requests, message => message.GetProperty("method").GetString() == "notifications/cancelled");
    }

    [Fact]
    public async Task ConcurrentUnicodeCalls_KeepIndependentRequestIds()
    {
        var handler = new McpHttpTestHandler();
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, 24).Select(index => client.CallToolAsync("echo",
            JsonSerializer.SerializeToElement(new { text = $"中文 {index} 🙂" }), TestContext.Current.CancellationToken)));

        Assert.All(results, result => Assert.Equal("中文 🙂", result.Content));
        var calls = handler.Requests.Where(message => message.GetProperty("method").GetString() == "tools/call").ToArray();
        Assert.Equal(24, calls.Select(message => message.GetProperty("id").GetInt64()).Distinct().Count());
        Assert.True(handler.SessionHeaderObserved);
    }
}
