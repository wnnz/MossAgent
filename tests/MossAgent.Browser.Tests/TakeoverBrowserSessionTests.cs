using MossAgent.Browser;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class TakeoverBrowserSessionTests
{
    [Fact]
    public async Task HandedToUser_RejectsEveryOperationWithTakeoverError()
    {
        var (manager, external) = CreateManager();
        await using var _ = manager;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);

        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);

        foreach (var operation in Operations(session))
        {
            var exception = await Assert.ThrowsAsync<BrowserTakeoverException>(operation);
            Assert.Equal("browser_takeover_active", exception.ErrorCode);
            Assert.Equal(BrowserControlState.HandedToUser, exception.State);
        }

        Assert.Equal(BrowserControlState.HandedToUser, manager.GetControlState(taskId));
        Assert.Equal(1, external.CreateCount);
    }

    [Fact]
    public async Task UserTakeover_CancelsInFlightAutomationCall()
    {
        var probe = new ProbeBrowserSession { BlockNavigate = true };
        var (manager, external) = CreateManager();
        await using var _ = manager;
        external.SessionSource = () => probe;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        var inFlight = session.NavigateAsync(new Uri("https://example.com"), CancellationToken.None);
        await probe.NavigateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BrowserTakeoverException>(() => inFlight);
        Assert.Equal("browser_takeover_active", exception.ErrorCode);
    }

    [Fact]
    public async Task ReturnControl_RereadsInnerSessionThenAllowsOperations()
    {
        var probe = new ProbeBrowserSession();
        var (manager, external) = CreateManager();
        await using var _ = manager;
        external.SessionSource = () => probe;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);
        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);

        var result = await manager.ReturnControlAsync(taskId, CancellationToken.None);

        Assert.Equal(BrowserControlState.Automated, result);
        Assert.Equal(1, probe.ListTabsCalls);
        await session.GetPageTextAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FailedReturn_KeepsHandedToUserAndRejectsOperations()
    {
        var probe = new ProbeBrowserSession
        {
            ListTabsFailure = new InvalidOperationException("still busy")
        };
        var (manager, external) = CreateManager();
        await using var _ = manager;
        external.SessionSource = () => probe;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);
        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);

        var result = await manager.ReturnControlAsync(taskId, CancellationToken.None);

        Assert.Equal(BrowserControlState.HandedToUser, result);
        var exception = await Assert.ThrowsAsync<BrowserTakeoverException>(
            () => session.GetPageTextAsync(CancellationToken.None));
        Assert.Equal("browser_takeover_active", exception.ErrorCode);
    }

    [Fact]
    public async Task DisconnectedInnerSession_MarksDisconnectedAndRejectsOperations()
    {
        var probe = new ProbeBrowserSession
        {
            ListTabsFailure = new ObjectDisposedException("session")
        };
        var (manager, external) = CreateManager();
        await using var _ = manager;
        external.SessionSource = () => probe;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);
        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);

        var result = await manager.ReturnControlAsync(taskId, CancellationToken.None);

        Assert.Equal(BrowserControlState.Disconnected, result);
        var exception = await Assert.ThrowsAsync<BrowserTakeoverException>(
            () => session.GetPageTextAsync(CancellationToken.None));
        Assert.Equal("browser_session_disconnected", exception.ErrorCode);
        Assert.Equal(BrowserControlState.Disconnected, exception.State);
    }

    [Fact]
    public async Task DeadInnerSession_MarksDisconnectedAndRejectsWithDisconnectedCode()
    {
        var probe = new ProbeBrowserSession();
        var (manager, external) = CreateManager();
        await using var _ = manager;
        external.SessionSource = () => probe;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        var raised = new List<BrowserControlState>();
        manager.StateChanged += (_, args) =>
        {
            if (args.TaskId == taskId) raised.Add(args.State);
        };
        await session.GetPageTextAsync(CancellationToken.None);
        probe.OperationFailure = new ObjectDisposedException("browser");

        var exception = await Assert.ThrowsAsync<BrowserTakeoverException>(
            () => session.GetPageTextAsync(CancellationToken.None));

        Assert.Equal("browser_session_disconnected", exception.ErrorCode);
        Assert.Equal(BrowserControlState.Disconnected, manager.GetControlState(taskId));
        Assert.Contains(BrowserControlState.Disconnected, raised);
        var next = await Assert.ThrowsAsync<BrowserTakeoverException>(
            () => session.NavigateAsync(new Uri("https://example.com"), CancellationToken.None));
        Assert.Equal("browser_session_disconnected", next.ErrorCode);
    }

    [Fact]
    public async Task ReturnWithoutCreatedSession_SucceedsWithoutLaunchingBrowser()
    {
        var (manager, external) = CreateManager();
        await using var _ = manager;
        var taskId = Guid.NewGuid();
        manager.GetLazySession(taskId);
        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);

        var result = await manager.ReturnControlAsync(taskId, CancellationToken.None);

        Assert.Equal(BrowserControlState.Automated, result);
        Assert.Equal(0, external.CreateCount);
    }

    [Fact]
    public async Task Takeover_IsIsolatedPerTask()
    {
        var (manager, external) = CreateManager();
        await using var _ = manager;
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var first = manager.GetLazySession(firstId);
        var second = manager.GetLazySession(secondId);
        await second.GetPageTextAsync(CancellationToken.None);

        await manager.BeginUserTakeoverAsync(firstId, CancellationToken.None);

        await Assert.ThrowsAsync<BrowserTakeoverException>(
            () => first.GetPageTextAsync(CancellationToken.None));
        await second.GetPageTextAsync(CancellationToken.None);
        Assert.Equal(BrowserControlState.HandedToUser, manager.GetControlState(firstId));
        Assert.Equal(BrowserControlState.Automated, manager.GetControlState(secondId));
        Assert.Equal(1, external.CreateCount);
    }

    [Fact]
    public async Task ReleaseAsync_ClearsTakeoverStateAndRaisesAutomated()
    {
        var (manager, _) = CreateManager();
        await using var _1 = manager;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);
        var raised = new List<BrowserControlState>();
        manager.StateChanged += (_, args) =>
        {
            if (args.TaskId == taskId) raised.Add(args.State);
        };
        await manager.BeginUserTakeoverAsync(taskId, CancellationToken.None);

        await manager.ReleaseAsync(taskId);

        Assert.False(manager.HasControlState(taskId));
        Assert.Equal(BrowserControlState.Automated, manager.GetControlState(taskId));
        Assert.Equal(
            [BrowserControlState.HandedToUser, BrowserControlState.Automated], raised);
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => session.GetPageTextAsync(CancellationToken.None));
    }

    private static IEnumerable<Func<Task>> Operations(IBrowserSession session)
    {
        var token = CancellationToken.None;
        yield return () => session.ListTabsAsync(token);
        yield return () => session.NewTabAsync(null, token);
        yield return () => session.SwitchTabAsync("tab-1", token);
        yield return () => session.CloseTabAsync("tab-1", token);
        yield return () => session.NavigateAsync(new Uri("https://example.com"), token);
        yield return () => session.GoBackAsync(token);
        yield return () => session.GoForwardAsync(token);
        yield return () => session.ReloadAsync(token);
        yield return () => session.OpenDeveloperToolsAsync(token);
        yield return () => session.GetPageTextAsync(token);
        yield return () => session.GetSnapshotAsync(token);
        yield return () => session.ClickAsync("#a", token);
        yield return () => session.TypeAsync("#a", "x", token);
        yield return () => session.SelectOptionAsync("#a", "x", token);
        yield return () => session.WaitForAsync("#a", BrowserElementState.Visible, TimeSpan.FromSeconds(1), token);
        yield return () => session.ScrollAsync(null, 0, 10, token);
        yield return () => session.ScreenshotAsync(false, token);
        yield return () => session.DownloadAsync("#a", Stream.Null, token);
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
