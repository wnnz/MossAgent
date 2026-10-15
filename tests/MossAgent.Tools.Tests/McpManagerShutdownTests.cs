using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Mcp;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpManagerShutdownTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownRetainsPendingReadsAndRejectsLateProfilesAndQueuedReloads(bool lateProfile)
    {
        var configurations = new McpShutdownConfigurationRepository();
        var tools = new ToolRegistry([new ApprovalExecutionProbeTool()]);
        await using var manager = CreateManager(configurations, tools);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var first = manager.ReloadAsync(cancellation.Token);
        await configurations.ReadStarted.Task.WaitAsync(cancellation.Token);
        var queued = manager.ReloadAsync(cancellation.Token);
        try
        {
            var readCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = configurations.ReadToken.Register(() => readCancelled.TrySetResult());
            var disposal = manager.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(2), cancellation.Token));
            await readCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellation.Token);
            Assert.True(configurations.ReadToken.IsCancellationRequested);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.ReloadAsync(cancellation.Token));
            Assert.Same(disposal, manager.DisposeAsync().AsTask());
            IReadOnlyList<McpServerProfile> profiles = lateProfile
                ? [new(Guid.NewGuid(), "late", McpTransportKind.Stdio, "not-launched", "[]", null, "{}", null, "{}", null, true)] : [];
            configurations.ReadReleased.TrySetResult(profiles);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            await disposal.WaitAsync(TimeSpan.FromSeconds(2), cancellation.Token);
            Assert.Equal(1, configurations.ReadCount);
            Assert.Empty(manager.Errors);
            Assert.Equal("approval.probe", Assert.Single(tools.Descriptors).Name);
        }
        finally
        {
            cancellation.Cancel();
            configurations.ReadReleased.TrySetResult([]);
            await Task.WhenAll(first, queued).WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken).ConfigureAwait(
                ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        }
    }

    [Fact]
    public async Task CancellationOfAQueuedReloadDoesNotCancelTheCurrentRead()
    {
        var configurations = new McpShutdownConfigurationRepository();
        var tools = new ToolRegistry([]);
        await using var manager = CreateManager(configurations, tools);
        var token = TestContext.Current.CancellationToken;
        var first = manager.ReloadAsync(token);
        await configurations.ReadStarted.Task.WaitAsync(token);
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var queued = manager.ReloadAsync(cancellation.Token);
            cancellation.Cancel();
            var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            Assert.Equal(cancellation.Token, failure.CancellationToken);
            Assert.False(configurations.ReadToken.IsCancellationRequested);
            configurations.ReadReleased.TrySetResult([]);
            await first;
            Assert.Equal(1, configurations.ReadCount);
            Assert.Empty(manager.Errors);
        }
        finally
        {
            configurations.ReadReleased.TrySetResult([]);
            await first.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        }
    }

    private static McpManager CreateManager(McpShutdownConfigurationRepository configurations, ToolRegistry tools)
    {
        var paths = new AppDataPaths(Path.Combine(Path.GetTempPath(), "MossAgent-mcp-shutdown", Guid.NewGuid().ToString("N")));
        return new McpManager(configurations, new McpHttpClientFactory(new SqliteConfigurationRepository(
            new SqliteConnectionFactory(paths))), tools, new RejectingTaskProcessLauncher());
    }
}
