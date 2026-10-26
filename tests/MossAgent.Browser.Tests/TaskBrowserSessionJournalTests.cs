using System.Security.Cryptography;
using System.Text;
using MossAgent.Application.Persistence;
using MossAgent.Browser;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class TaskBrowserSessionJournalTests
{
    [Fact]
    public async Task SessionIdentity_IsJournaledAfterCreationBeforeAnyIntent()
    {
        var (manager, _, journal) = CreateManager();
        await using var _ = manager;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);

        await session.NavigateAsync(new Uri("https://example.com"), CancellationToken.None);

        var recorded = Assert.Single(journal.Sessions);
        Assert.Equal(taskId, recorded.TaskId);
        Assert.Equal(BrowserEngine.Chrome, recorded.Engine);
        Assert.Equal(BrowserProfileMode.Managed, recorded.ProfileMode);
        // The session row precedes every committed intent so actions are never orphaned.
        Assert.Equal("session", journal.Sequence[0]);
        Assert.Equal(["session", "action", "resolve"], journal.Sequence.Take(3));
    }

    [Fact]
    public async Task ReadOnlyCalls_NeverRegisterActionIntents()
    {
        var (manager, _, journal) = CreateManager();
        await using var _ = manager;
        var session = manager.GetLazySession(Guid.NewGuid());
        var token = CancellationToken.None;

        await session.GetPageTextAsync(token);
        await session.GetSnapshotAsync(token);
        await session.ListTabsAsync(token);
        await session.SwitchTabAsync("tab-1", token);
        await session.WaitForAsync("#a", BrowserElementState.Visible, TimeSpan.FromSeconds(1), token);
        await session.ScrollAsync("#a", 0, 10, token);
        await session.ScreenshotAsync(false, token);
        await Assert.ThrowsAsync<NotSupportedException>(() => session.OpenDeveloperToolsAsync(token));

        Assert.Empty(journal.Actions);
        // The lazy session still materialized, so its identity was journaled once.
        Assert.Single(journal.Sessions);
    }

    [Fact]
    public async Task EveryMutatingCall_RegistersCommittedIntentThenResolves()
    {
        var (manager, _, journal) = CreateManager();
        await using var _ = manager;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        var token = CancellationToken.None;

        await session.NewTabAsync(null, token);
        await session.CloseTabAsync("tab-2", token);
        await session.NavigateAsync(new Uri("https://example.com/page?q=1"), token);
        await session.GoBackAsync(token);
        await session.GoForwardAsync(token);
        await session.ReloadAsync(token);
        await session.ClickAsync("#button", token);
        await session.TypeAsync("#input", "s3cret-typed-text", token);
        await session.SelectOptionAsync("#select", "option", token);
        await session.DownloadAsync("#download", Stream.Null, token);

        var kinds = journal.Actions.Select(action => action.Kind).ToArray();
        Assert.Equal(
        [
            BrowserActionKind.NewTab, BrowserActionKind.CloseTab, BrowserActionKind.Navigate,
            BrowserActionKind.NavigateBack, BrowserActionKind.NavigateForward,
            BrowserActionKind.Reload, BrowserActionKind.Click, BrowserActionKind.Type,
            BrowserActionKind.SelectOption, BrowserActionKind.Download
        ], kinds);
        Assert.All(journal.Actions, action => Assert.Equal(taskId, action.TaskId));
        Assert.Equal(10, journal.Resolutions.Count);
        Assert.All(journal.Resolutions,
            resolution => Assert.Equal(BrowserActionIntentState.Resolved, resolution.State));
        // Committed intents always precede their resolution in the journal sequence.
        var expected = new List<string> { "session" };
        for (var index = 0; index < 10; index++)
        {
            expected.Add("action");
            expected.Add("resolve");
        }

        Assert.Equal(expected, journal.Sequence);
    }

    [Fact]
    public async Task NavigateTargetHash_CoversOriginOnly_NeverTheFullUrl()
    {
        var (manager, _, journal) = CreateManager();
        await using var _ = manager;
        var session = manager.GetLazySession(Guid.NewGuid());

        await session.NavigateAsync(
            new Uri("https://user:password@Example.COM:8443/deep/path?query=secret#frag"),
            CancellationToken.None);

        var action = Assert.Single(journal.Actions);
        Assert.Equal(BrowserActionKind.Navigate, action.Kind);
        Assert.Equal(Hash("https://example.com:8443"), action.TargetHash);
        Assert.Equal(16, action.TargetHash?.Length);
        Assert.DoesNotContain("password", action.TargetHash);
        Assert.DoesNotContain("deep", action.TargetHash);
        Assert.DoesNotContain("query", action.TargetHash);
        Assert.DoesNotContain('/', action.TargetHash!);
        Assert.DoesNotContain(':', action.TargetHash!);
    }

    [Fact]
    public async Task SelectorTargetsAreHashed_TypedTextNeverReachesTheJournal()
    {
        var (manager, _, journal) = CreateManager();
        await using var _ = manager;
        var session = manager.GetLazySession(Guid.NewGuid());

        await session.ClickAsync("#submit-btn", CancellationToken.None);
        await session.TypeAsync("#password", "hunter2-p@ssword", CancellationToken.None);

        Assert.Equal(2, journal.Actions.Count);
        Assert.Equal(Hash("#submit-btn"), journal.Actions[0].TargetHash);
        // The type intent stores the selector hash only; the typed text is never journaled.
        Assert.Equal(Hash("#password"), journal.Actions[1].TargetHash);
        Assert.DoesNotContain("hunter2", journal.Actions[1].TargetHash);
        Assert.DoesNotContain('#', journal.Actions[1].TargetHash!);
    }

    [Fact]
    public async Task FailingOperation_ResolvesIntentAsFailed()
    {
        var (manager, _, journal) = CreateManager();
        await using var _ = manager;
        var session = manager.GetLazySession(Guid.NewGuid());

        // The stub session refuses to close its last remaining tab.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.CloseTabAsync("tab-1", CancellationToken.None));

        var resolution = Assert.Single(journal.Resolutions);
        Assert.Equal(BrowserActionIntentState.Failed, resolution.State);
    }

    [Fact]
    public async Task CancelledOperation_ResolvesIntentAsCancelled()
    {
        var probe = new ProbeBrowserSession { BlockNavigate = true };
        var (manager, external, journal) = CreateManager();
        await using var _ = manager;
        external.SessionSource = () => probe;
        var session = manager.GetLazySession(Guid.NewGuid());
        using var cancellation = new CancellationTokenSource();
        var inFlight = session.NavigateAsync(new Uri("https://example.com"), cancellation.Token);
        await probe.NavigateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inFlight);
        var resolution = Assert.Single(journal.Resolutions);
        Assert.Equal(BrowserActionIntentState.Cancelled, resolution.State);
    }

    [Fact]
    public async Task MissingAttachedSessionRow_FailsClosedWithoutSideEffect()
    {
        var (manager, external, journal) = CreateManager();
        await using var _ = manager;
        var probe = new ProbeBrowserSession();
        external.SessionSource = () => probe;
        journal.HasAttachedSession = false;
        var session = manager.GetLazySession(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.NavigateAsync(new Uri("https://example.com"), CancellationToken.None));

        // The intent was attempted but rejected; the side effect never reached the page.
        Assert.Single(journal.Actions);
        Assert.Null(probe.CurrentUri);
        Assert.Empty(journal.Resolutions);
    }

    [Fact]
    public async Task ActionJournalFailure_FailsClosedWithoutSideEffect()
    {
        var (manager, external, journal) = CreateManager();
        await using var _ = manager;
        var probe = new ProbeBrowserSession();
        external.SessionSource = () => probe;
        journal.ActionFailure = new InvalidOperationException("journal write failed");
        var session = manager.GetLazySession(Guid.NewGuid());

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.ClickAsync("#a", CancellationToken.None));

        Assert.Equal("journal write failed", thrown.Message);
        Assert.Null(probe.CurrentUri);
    }

    [Fact]
    public async Task SessionRegistrationRejection_DisposesNewSessionAndThrows()
    {
        var (manager, external, journal) = CreateManager();
        await using var _ = manager;
        journal.SessionResult = false;
        var session = manager.GetLazySession(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetPageTextAsync(CancellationToken.None));

        Assert.True(external.LastSession?.IsDisposed);
        Assert.Empty(journal.Actions);
    }

    [Fact]
    public async Task SessionRegistrationFailure_DisposesNewSessionAndPropagates()
    {
        var (manager, external, journal) = CreateManager();
        await using var _ = manager;
        journal.SessionFailure = new InvalidOperationException("database unavailable");
        var session = manager.GetLazySession(Guid.NewGuid());

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.NavigateAsync(new Uri("https://example.com"), CancellationToken.None));

        Assert.Equal("database unavailable", thrown.Message);
        Assert.True(external.LastSession?.IsDisposed);
        Assert.Empty(journal.Actions);
    }

    [Fact]
    public async Task DeadInnerSession_RecordsDisconnectedBestEffort()
    {
        var probe = new ProbeBrowserSession();
        var (manager, external, journal) = CreateManager();
        await using var _ = manager;
        external.SessionSource = () => probe;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);
        probe.OperationFailure = new ObjectDisposedException("browser");

        await Assert.ThrowsAsync<BrowserTakeoverException>(
            () => session.GetPageTextAsync(CancellationToken.None));

        Assert.Contains(taskId, journal.Disconnected);
        // The read-only call itself was never journaled as an action intent.
        Assert.Empty(journal.Actions);
    }

    [Fact]
    public async Task ReleaseAsync_RecordsDetachedBestEffort()
    {
        var (manager, _, journal) = CreateManager();
        await using var _ = manager;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);

        await manager.ReleaseAsync(taskId);

        Assert.Contains(taskId, journal.Detached);
    }

    private static (TaskBrowserSessionManager Manager, RecordingBrowserSessionFactory External,
        RecordingBrowserSessionJournal Journal) CreateManager()
    {
        var external = new RecordingBrowserSessionFactory();
        var journal = new RecordingBrowserSessionJournal();
        var manager = new TaskBrowserSessionManager(
            external,
            new RecordingBrowserSessionFactory(),
            new StubBrowserConfigurationRepository(
                new BrowserConfiguration(
                    DefaultBrowserKind.Chrome, BrowserProfilePreference.Managed,
                    null, null, null)),
            new StubConfigurationRepository(),
            new StubBrowserProfilePaths(),
            new StubBrowserProfileAccessApproval(),
            journal);
        return (manager, external, journal);
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}
