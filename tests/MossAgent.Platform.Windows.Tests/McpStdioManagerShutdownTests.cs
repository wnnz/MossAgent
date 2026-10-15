using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class McpStdioManagerShutdownTests
{
    [Fact]
    public async Task ShutdownDuringInitializationCancelsCurrentAndQueuedReloadsAndReclaimsDescendants()
    {
        await using var fixture = await McpStdioManagerFixture.CreateAsync("hang-initialize");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Server.Token);
        var first = fixture.Manager.ReloadAsync(cancellation.Token);
        var processes = await fixture.Server.BindProcessesAsync();
        var queued = fixture.Manager.ReloadAsync(cancellation.Token);
        try
        {
            var disposal = fixture.Manager.DisposeAsync().AsTask();
            Assert.Same(disposal, fixture.Manager.DisposeAsync().AsTask());
            await disposal.WaitAsync(fixture.Server.Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            await fixture.Server.AssertExitedAsync(processes);
            Assert.Empty(fixture.Tools.Descriptors);
            Assert.Empty(fixture.Manager.Errors);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Manager.ReloadAsync(fixture.Server.Token));
        }
        finally
        {
            cancellation.Cancel();
            fixture.Clock.Advance(TimeSpan.FromSeconds(3));
            await Task.WhenAll(first, queued).WaitAsync(fixture.Server.Token).ConfigureAwait(
                ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        }
    }

    [Fact]
    public async Task ShutdownAfterDiscoveryUnregistersToolsAndReclaimsTheWholeProcessTree()
    {
        await using var fixture = await McpStdioManagerFixture.CreateAsync();
        await fixture.Manager.ReloadAsync(fixture.Server.Token);
        var processes = await fixture.Server.BindProcessesAsync();
        Assert.Single(fixture.Tools.Descriptors);
        Assert.Empty(fixture.Manager.Errors);

        await fixture.Manager.DisposeAsync();

        Assert.Empty(fixture.Tools.Descriptors);
        await fixture.Server.AssertExitedAsync(processes);
        await fixture.Manager.DisposeAsync();
    }
}
