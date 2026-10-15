using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class McpSynchronizationContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockingUiCaller_DoesNotPreventMcpInitialization(bool manager)
    {
        await using var fixture = await McpStdioManagerFixture.CreateAsync();
        var context = new McpNonPumpingSynchronizationContext();
        var initialization = Task.Run(() =>
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                if (manager) fixture.Manager.ReloadAsync(fixture.Server.Token).GetAwaiter().GetResult();
                else fixture.Server.Client.InitializeAsync(fixture.Server.Token).GetAwaiter().GetResult();
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }, fixture.Server.Token);
        try
        {
            await initialization.WaitAsync(fixture.Server.Token);
            Assert.Equal(0, context.Posts);
            Assert.Empty(fixture.Manager.Errors);
            if (manager) Assert.Single(fixture.Tools.Descriptors);
            else Assert.Equal("echo", Assert.Single(await fixture.Server.Client.ListToolsAsync(fixture.Server.Token)).Name);
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!initialization.IsCompleted)
            {
                context.Drain();
                await Task.Delay(10, cleanup.Token);
            }
            await initialization.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing
                | ConfigureAwaitOptions.ContinueOnCapturedContext);
        }
    }
}
