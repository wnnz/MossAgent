using Avalonia.Headless;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class CompatibleStreamServerLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public async Task IdleServer_CanStopAfterTheHeadlessCallbackReturns()
    {
        var server = await _session.Dispatch(() => new CompatibleStreamFlowServer(
            "unused-fixture.txt", CompatibleStreamScenario.PrematureEof, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        await server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Empty(server.Requests);
    }
}
