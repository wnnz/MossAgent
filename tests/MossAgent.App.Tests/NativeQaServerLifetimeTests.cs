using System.Text.Json;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class NativeQaServerLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task McpServer_CreationAndShutdownDoNotDependOnTheUiContext(bool createOnContext)
    {
        var context = new RecordingServerSynchronizationContext();
        var server = Create(() => new WorkspaceMcpHttpServer(), createOnContext ? context : null);
        await AssertContextIndependentDisposalAsync(server, server.ShutdownToken, context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModelServer_CreationAndShutdownDoNotDependOnTheUiContext(bool createOnContext)
    {
        var context = new RecordingServerSynchronizationContext();
        var server = Create(() => new WorkspaceToolCallModelServer("mcp.workflow.echo",
            JsonSerializer.SerializeToElement(new { message = "safe" }), TestContext.Current.CancellationToken),
            createOnContext ? context : null);
        await AssertContextIndependentDisposalAsync(server, server.ShutdownToken, context);
    }

    private static async Task AssertContextIndependentDisposalAsync(IAsyncDisposable server, CancellationToken shutdown,
        RecordingServerSynchronizationContext context)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callback = shutdown.Register(() =>
        {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
        });
        var disposal = Create(() => server.DisposeAsync().AsTask(), context);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally { release.TrySetResult(); }
        await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(0, context.Posts);
    }

    private static TService Create<TService>(Func<TService> create, SynchronizationContext? context)
    {
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            return create();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
}
