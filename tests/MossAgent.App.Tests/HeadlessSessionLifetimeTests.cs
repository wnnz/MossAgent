using Avalonia.Headless;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class HeadlessSessionLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImmediateDisposal_RetainsTheConsumerTaskAndStopsTheQueue(bool asynchronous)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        for (var iteration = 0; iteration < 256; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
            if (asynchronous) await session.DisposeAsync();
            else session.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                session.DispatchAsync(() => Task.CompletedTask, cancellationToken));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedAndFailedCallbacks_KeepTheNextApplicationScopeIsolated(bool failure)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
        Avalonia.Application? previous = null;
        var callback = session.DispatchAsync(() =>
        {
            previous = Assert.IsType<SharedVisualTestApplication>(Avalonia.Application.Current);
            previous.Resources["lifetime-marker"] = "synthetic state";
            return failure ? Task.FromException(new InvalidOperationException("synthetic callback failure")) : Task.CompletedTask;
        }, cancellationToken);
        if (failure) await Assert.ThrowsAsync<InvalidOperationException>(() => callback);
        else await callback;

        await session.DispatchAsync(() =>
        {
            var current = Assert.IsType<SharedVisualTestApplication>(Avalonia.Application.Current);
            Assert.NotSame(previous, current);
            Assert.False(current.Resources.ContainsKey("lifetime-marker"));
            return Task.CompletedTask;
        }, cancellationToken);
    }
}
