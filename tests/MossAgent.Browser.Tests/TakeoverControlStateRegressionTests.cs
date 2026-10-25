using MossAgent.Browser;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class TakeoverControlStateRegressionTests
{
    [Fact]
    public async Task CallerCancellationDuringReturnProbe_CompletesReturnAsHandedToUser()
    {
        var probe = new ProbeBrowserSession { BlockListTabs = true };
        var (manager, external) = CreateManager();
        await using var _ = manager;
        external.SessionSource = () => probe;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);
        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var returning = manager.ReturnControlAsync(taskId, cancellation.Token);
        await probe.ListTabsStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => returning);
        Assert.Equal(BrowserControlState.HandedToUser, manager.GetControlState(taskId));

        probe.BlockListTabs = false;
        var result = await manager.ReturnControlAsync(taskId, CancellationToken.None);

        Assert.Equal(BrowserControlState.Automated, result);
        await session.GetPageTextAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CallerCancellationDuringReturnProbe_StillPermitsNewTakeover()
    {
        var probe = new ProbeBrowserSession { BlockListTabs = true };
        var (manager, external) = CreateManager();
        await using var _ = manager;
        external.SessionSource = () => probe;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);
        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var returning = manager.ReturnControlAsync(taskId, cancellation.Token);
        await probe.ListTabsStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => returning);
        probe.BlockListTabs = false;

        Assert.Equal(
            BrowserControlState.Automated,
            await manager.ReturnControlAsync(taskId, CancellationToken.None));
        Assert.Equal(
            BrowserControlState.HandedToUser,
            await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None));
        Assert.Equal(BrowserControlState.HandedToUser, manager.GetControlState(taskId));
    }

    [Fact]
    public async Task HasControlState_FalseUntilSessionCreatedOrTakeover()
    {
        var (manager, _) = CreateManager();
        await using var _1 = manager;
        var taskId = Guid.NewGuid();

        manager.GetLazySession(taskId);
        Assert.False(manager.HasControlState(taskId));
        Assert.Equal(BrowserControlState.Automated, manager.GetControlState(taskId));

        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);
        Assert.True(manager.HasControlState(taskId));
    }

    [Fact]
    public async Task TakeoverBeforeSessionExists_ExposesControlState()
    {
        var (manager, _) = CreateManager();
        await using var _ = manager;
        var taskId = Guid.NewGuid();

        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);

        Assert.True(manager.HasControlState(taskId));
        Assert.Equal(BrowserControlState.HandedToUser, manager.GetControlState(taskId));
    }

    [Fact]
    public async Task DisposeAsync_ReleasesTakeoverOnlyTaskState()
    {
        var manager = CreateManager().Manager;
        var taskId = Guid.NewGuid();
        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);
        Assert.True(manager.HasControlState(taskId));

        await manager.DisposeAsync();

        Assert.False(manager.HasControlState(taskId));
        Assert.Equal(BrowserControlState.Automated, manager.GetControlState(taskId));
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);
    }

    private static (TaskBrowserSessionManager Manager, RecordingBrowserSessionFactory External)
        CreateManager()
    {
        var external = new RecordingBrowserSessionFactory();
        var embedded = new RecordingBrowserSessionFactory();
        var manager = new TaskBrowserSessionManager(
            external,
            embedded,
            new StubBrowserConfigurationRepository(
                new BrowserConfiguration(
                    DefaultBrowserKind.Chrome, BrowserProfilePreference.Managed,
                    null, null, null)),
            new StubConfigurationRepository(),
            new StubBrowserProfilePaths(),
            new StubBrowserProfileAccessApproval());
        return (manager, external);
    }
}
