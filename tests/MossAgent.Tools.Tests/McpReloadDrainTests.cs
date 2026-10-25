using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpReloadDrainTests
{
    [Fact]
    public async Task Reload_DrainsInFlightCallOnOldClientBeforeReplacingIt()
    {
        var token = TestContext.Current.CancellationToken;
        await using var server = new McpHttpFixtureServer();
        await using var fixture = new McpToolRegistrationFixture();
        fixture.AddProfile("fixture", server.Endpoint);
        await fixture.Manager.ReloadAsync(token);
        var toolName = Assert.Single(fixture.Descriptors).Name;
        var callStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.BeforeCallResponse = async () =>
        {
            callStarted.TrySetResult();
            await callRelease.Task;
        };

        var executing = fixture.ExecuteAsync(toolName);
        await callStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        var reload = fixture.Manager.ReloadAsync(token);
        await WaitUntilAsync(() => fixture.Descriptors.Count == 0, token);

        Assert.False(reload.IsCompleted);
        callRelease.TrySetResult();
        await Task.WhenAll(executing, reload).WaitAsync(TimeSpan.FromSeconds(10), token);

        Assert.True((await executing).IsSuccess);
        Assert.Equal("echo", Assert.Single(server.Calls));
        Assert.True(server.DeleteObserved);
        Assert.Equal(toolName, Assert.Single(fixture.Descriptors).Name);
        Assert.True((await fixture.ExecuteAsync(toolName)).IsSuccess);
        Assert.Equal(2, server.Calls.Count);
    }

    [Fact]
    public async Task ReloadWindow_ToolUnavailableAndLateCallFailsAsReconnecting()
    {
        var token = TestContext.Current.CancellationToken;
        await using var server = new McpHttpFixtureServer();
        await using var fixture = new McpToolRegistrationFixture();
        fixture.AddProfile("fixture", server.Endpoint);
        await fixture.Manager.ReloadAsync(token);
        var toolName = Assert.Single(fixture.Descriptors).Name;
        Assert.True(fixture.Tools.TryGet(toolName, out var retained));
        var callStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.BeforeCallResponse = async () =>
        {
            callStarted.TrySetResult();
            await callRelease.Task;
        };

        var executing = fixture.ExecuteAsync(toolName);
        await callStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        var reload = fixture.Manager.ReloadAsync(token);
        await WaitUntilAsync(() => fixture.Descriptors.Count == 0, token);
        await Task.Delay(100, token);

        var unavailable = await fixture.ExecuteAsync(toolName);
        Assert.False(unavailable.IsSuccess);
        Assert.Equal("tool_not_found", unavailable.ErrorCode);
        var late = await retained!.ExecuteAsync(
            new ToolRequest("late-call", toolName, JsonSerializer.SerializeToElement(new { text = "late" })),
            new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [], ApprovalPolicy.FullAccess), token);
        Assert.False(late.IsSuccess);
        Assert.Equal("mcp_reconnecting", late.ErrorCode);
        Assert.Contains("重新连接", late.Summary);
        Assert.False(reload.IsCompleted);

        callRelease.TrySetResult();
        await Task.WhenAll(executing, reload).WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.Single(server.Calls);
        Assert.Equal(toolName, Assert.Single(fixture.Descriptors).Name);
    }

    [Fact]
    public async Task QueuedReloads_RunSequentiallyThroughTheReadGate()
    {
        var token = TestContext.Current.CancellationToken;
        var configurations = new McpShutdownConfigurationRepository();
        var tools = new ToolRegistry([]);
        var paths = new AppDataPaths(
            Path.Combine(Path.GetTempPath(), "MossAgent-mcp-drain", Guid.NewGuid().ToString("N")));
        await using var manager = new McpManager(configurations,
            new McpHttpClientFactory(new SqliteConfigurationRepository(new SqliteConnectionFactory(paths))),
            tools, new RejectingTaskProcessLauncher());

        var first = manager.ReloadAsync(token);
        await configurations.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        var second = manager.ReloadAsync(token);
        await Task.Delay(100, token);

        Assert.Equal(1, configurations.ReadCount);
        configurations.ReadReleased.TrySetResult([]);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.Equal(2, configurations.ReadCount);
        Assert.Empty(manager.Errors);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(15, timeout.Token);
    }
}
