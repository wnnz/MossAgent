using MossAgent.Browser;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class TaskBrowserSessionApprovalTests
{
    [Theory]
    [InlineData(BrowserProfilePreference.DailyProfile, BrowserProfileMode.DailyProfile)]
    [InlineData(BrowserProfilePreference.ExistingCdp, BrowserProfileMode.ExistingCdp)]
    public async Task SensitiveProfile_RequestsApprovalBeforeLaunch(
        BrowserProfilePreference preference,
        BrowserProfileMode expectedMode)
    {
        var approvals = new StubBrowserProfileAccessApproval();
        var (manager, external) = CreateManager(preference, approvals);
        await using var _ = manager;
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);

        await session.GetPageTextAsync(CancellationToken.None);

        var call = Assert.Single(approvals.Calls);
        Assert.Equal(taskId, call.TaskId);
        Assert.Equal(expectedMode, call.Mode);
        Assert.Equal(1, external.CreateCount);
        Assert.Equal(expectedMode, external.LastOptions?.ProfileMode);
    }

    [Theory]
    [InlineData(BrowserProfilePreference.DailyProfile)]
    [InlineData(BrowserProfilePreference.ExistingCdp)]
    public async Task DeniedApproval_PreventsFactoryAndThrowsDenied(BrowserProfilePreference preference)
    {
        var approvals = new StubBrowserProfileAccessApproval { Decision = _ => false };
        var (manager, external) = CreateManager(preference, approvals);
        await using var _ = manager;
        var session = manager.GetLazySession(Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<BrowserProfileAccessDeniedException>(
            () => session.GetPageTextAsync(CancellationToken.None));

        Assert.Equal("browser_profile_denied", exception.ErrorCode);
        Assert.Equal(0, external.CreateCount);
    }

    [Fact]
    public async Task ManagedProfile_NeverRequestsApproval()
    {
        var approvals = new StubBrowserProfileAccessApproval { Decision = _ => false };
        var (manager, external) = CreateManager(BrowserProfilePreference.Managed, approvals);
        await using var _ = manager;
        var session = manager.GetLazySession(Guid.NewGuid());

        await session.GetPageTextAsync(CancellationToken.None);

        Assert.Empty(approvals.Calls);
        Assert.Equal(1, external.CreateCount);
        Assert.Equal(BrowserProfileMode.Managed, external.LastOptions?.ProfileMode);
    }

    [Fact]
    public async Task Approval_IsCachedForTheManagerEntryLifetime()
    {
        var approvals = new StubBrowserProfileAccessApproval();
        var (manager, external) = CreateManager(BrowserProfilePreference.DailyProfile, approvals);
        await using var _ = manager;
        var session = manager.GetLazySession(Guid.NewGuid());

        await session.GetPageTextAsync(CancellationToken.None);
        await session.NavigateAsync(new Uri("https://example.com"), CancellationToken.None);
        await session.ScreenshotAsync(false, CancellationToken.None);

        Assert.Single(approvals.Calls);
        Assert.Equal(1, external.CreateCount);
    }

    [Fact]
    public async Task FailedLaunchRetry_DoesNotAskAgainWhenApproved()
    {
        var approvals = new StubBrowserProfileAccessApproval();
        var (manager, external) = CreateManager(BrowserProfilePreference.DailyProfile, approvals);
        await using var _ = manager;
        var session = manager.GetLazySession(Guid.NewGuid());
        external.FailNextCreate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetPageTextAsync(CancellationToken.None));
        await session.GetPageTextAsync(CancellationToken.None);

        Assert.Single(approvals.Calls);
        Assert.Equal(1, external.CreateCount);
    }

    [Fact]
    public async Task Denial_IsNotCached_AndRetryAsksAgain()
    {
        var approvals = new StubBrowserProfileAccessApproval { Decision = _ => false };
        var (manager, external) = CreateManager(BrowserProfilePreference.DailyProfile, approvals);
        await using var _ = manager;
        var session = manager.GetLazySession(Guid.NewGuid());

        await Assert.ThrowsAsync<BrowserProfileAccessDeniedException>(
            () => session.GetPageTextAsync(CancellationToken.None));
        approvals.Decision = _ => true;
        await session.GetPageTextAsync(CancellationToken.None);

        Assert.Equal(2, approvals.Calls.Count);
        Assert.Equal(1, external.CreateCount);
    }

    [Fact]
    public async Task ReleaseThenReuse_AsksForApprovalAgain()
    {
        var approvals = new StubBrowserProfileAccessApproval();
        var (manager, external) = CreateManager(BrowserProfilePreference.DailyProfile, approvals);
        await using var _ = manager;
        var taskId = Guid.NewGuid();
        var first = manager.GetLazySession(taskId);
        await first.GetPageTextAsync(CancellationToken.None);

        await manager.ReleaseAsync(taskId);
        var resumed = manager.GetLazySession(taskId);
        await resumed.GetPageTextAsync(CancellationToken.None);

        Assert.Equal(2, approvals.Calls.Count);
        Assert.Equal(2, external.CreateCount);
    }

    private static (TaskBrowserSessionManager Manager, RecordingBrowserSessionFactory External)
        CreateManager(
            BrowserProfilePreference preference,
            StubBrowserProfileAccessApproval approvals)
    {
        var external = new RecordingBrowserSessionFactory();
        var embedded = new RecordingBrowserSessionFactory();
        var manager = new TaskBrowserSessionManager(
            external,
            embedded,
            new StubBrowserConfigurationRepository(
                new BrowserConfiguration(
                    DefaultBrowserKind.Chrome, preference, null, null, null)),
            new StubConfigurationRepository(),
            new StubBrowserProfilePaths(),
            approvals);
        return (manager, external);
    }
}
