using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceLoadCoordinatorTests
{
    [Fact]
    public async Task AnAwaitedRefreshCannotOvertakeAnEarlierInitialization()
    {
        var loads = new WorkspaceLoadCoordinator();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<string>();
        var initial = loads.RunAsync(async () =>
        {
            events.Add("initial-start");
            entered.TrySetResult();
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
            events.Add("initial-end");
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var refresh = loads.RunAsync(() => { events.Add("refresh"); return Task.CompletedTask; });
        try
        {
            Assert.False(refresh.IsCompleted);
            Assert.Equal(["initial-start"], events);
        }
        finally { release.TrySetResult(); }

        await Task.WhenAll(initial, refresh).WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["initial-start", "initial-end", "refresh"], events);
    }

    [Fact]
    public async Task EveryRequestedRefreshRunsInsteadOfJoiningAnOlderCatalogSnapshot()
    {
        var loads = new WorkspaceLoadCoordinator();
        var version = 0;
        var snapshots = new List<int>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initial = loads.RunAsync(async () =>
        {
            snapshots.Add(version);
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
        });
        version = 1;
        var refresh = loads.RunAsync(() => { snapshots.Add(version); return Task.CompletedTask; });
        release.TrySetResult();

        await Task.WhenAll(initial, refresh).WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal([0, 1], snapshots);
    }

    [Fact]
    public async Task AFailedLoadReleasesTheNextExplicitRefresh()
    {
        var loads = new WorkspaceLoadCoordinator();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initial = loads.RunAsync(async () =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
            throw new InvalidOperationException("synthetic initialization failure");
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var refreshed = false;
        var refresh = loads.RunAsync(() => { refreshed = true; return Task.CompletedTask; });
        release.TrySetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => initial);
        await refresh.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(refreshed);
    }
}
