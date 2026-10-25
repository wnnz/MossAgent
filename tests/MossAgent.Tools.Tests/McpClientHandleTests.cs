using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpClientHandleTests
{
    private static readonly JsonElement Arguments = JsonSerializer.SerializeToElement(new { text = "sample" });
    private static readonly McpToolDefinition Definition = new("echo", "Echo",
        JsonSerializer.SerializeToElement(new { type = "object" }), false);

    [Fact]
    public async Task DrainAsync_WaitsForInFlightCallToComplete()
    {
        var inner = new GatedMcpClient();
        var handle = new McpClientHandle(inner);
        var token = TestContext.Current.CancellationToken;
        var call = handle.CallToolAsync("echo", Arguments, token);
        await inner.CallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        var drain = handle.DrainAsync(TimeSpan.FromSeconds(10));

        Assert.False(drain.IsCompleted);
        inner.CallRelease.TrySetResult();
        Assert.Equal("done", (await call).Content);
        await drain;
        await handle.DisposeAsync();
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task CallStartedDuringDrain_FailsAsReconnectingWithoutDispatch()
    {
        var inner = new GatedMcpClient();
        var handle = new McpClientHandle(inner);
        var token = TestContext.Current.CancellationToken;
        var drain = handle.DrainAsync(TimeSpan.FromSeconds(10));
        await drain;

        await Assert.ThrowsAsync<McpReconnectingException>(
            () => handle.CallToolAsync("echo", Arguments, token));

        Assert.Equal(0, inner.Calls);
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task DrainTimeout_StillDisposesAndPendingCallFailsExplicitly()
    {
        var inner = new GatedMcpClient();
        var handle = new McpClientHandle(inner);
        var token = TestContext.Current.CancellationToken;
        var call = handle.CallToolAsync("echo", Arguments, token);
        await inner.CallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        await handle.DrainAsync(TimeSpan.FromMilliseconds(50));
        await handle.DisposeAsync();

        await Assert.ThrowsAsync<IOException>(() => call);
        Assert.True(inner.Disposed.Task.IsCompleted);
        await Assert.ThrowsAsync<McpReconnectingException>(
            () => handle.CallToolAsync("echo", Arguments, token));
    }

    [Fact]
    public async Task DisposedHandle_RejectsNewCallsAsReconnecting()
    {
        var handle = new McpClientHandle(new GatedMcpClient());
        await handle.DisposeAsync();

        await Assert.ThrowsAsync<McpReconnectingException>(
            () => handle.CallToolAsync("echo", Arguments, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Adapter_ReportsReconnectingAsToolErrorWithoutCrashing()
    {
        var inner = new GatedMcpClient();
        var handle = new McpClientHandle(inner);
        await handle.DrainAsync(TimeSpan.FromSeconds(10));
        var adapter = new McpToolAdapter("mcp.fixture.echo", "echo", Definition, handle);
        var context = new ToolExecutionContext(Guid.NewGuid(), Environment.CurrentDirectory, [],
            ApprovalPolicy.FullAccess);

        var result = await adapter.ExecuteAsync(new ToolRequest("call-1", adapter.Descriptor.Name, Arguments),
            context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("mcp_reconnecting", result.ErrorCode);
        Assert.Contains("重新连接", result.Summary);
        Assert.Equal(0, inner.Calls);
    }
}
