using MossAgent.Browser;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class TaskBrowserSessionManagerTests
{
    [Fact]
    public async Task EmbeddedConfiguration_UsesEmbeddedFactory()
    {
        var external = new RecordingBrowserSessionFactory();
        var embedded = new RecordingBrowserSessionFactory();
        await using var manager = CreateManager(DefaultBrowserKind.Embedded, external, embedded);

        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(CancellationToken.None);

        Assert.Equal(0, external.CreateCount);
        Assert.Equal(1, embedded.CreateCount);
        Assert.Equal(BrowserEngine.Embedded, embedded.LastOptions?.Engine);
        Assert.Equal(taskId, embedded.LastOptions?.TaskId);
    }

    [Theory]
    [InlineData(DefaultBrowserKind.Chrome, BrowserEngine.Chrome)]
    [InlineData(DefaultBrowserKind.Edge, BrowserEngine.Edge)]
    public async Task ExternalConfiguration_UsesPlaywrightFactory(
        DefaultBrowserKind browser,
        BrowserEngine expectedEngine)
    {
        var external = new RecordingBrowserSessionFactory();
        var embedded = new RecordingBrowserSessionFactory();
        await using var manager = CreateManager(browser, external, embedded);

        var session = manager.GetLazySession(Guid.NewGuid());
        await session.GetPageTextAsync(CancellationToken.None);

        Assert.Equal(1, external.CreateCount);
        Assert.Equal(0, embedded.CreateCount);
        Assert.Equal(expectedEngine, external.LastOptions?.Engine);
    }

    [Fact]
    public async Task ReleaseBeforeFirstUse_PreventsStaleSessionFromLaunching()
    {
        var external = new RecordingBrowserSessionFactory();
        var embedded = new RecordingBrowserSessionFactory();
        await using var manager = CreateManager(DefaultBrowserKind.Chrome, external, embedded);
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);

        await manager.ReleaseAsync(taskId);

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            session.GetPageTextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, external.CreateCount);
    }

    [Fact]
    public async Task ReleaseAfterFirstUse_DisposesSessionAndAllowsFreshResume()
    {
        var external = new RecordingBrowserSessionFactory();
        var embedded = new RecordingBrowserSessionFactory();
        await using var manager = CreateManager(DefaultBrowserKind.Chrome, external, embedded);
        var taskId = Guid.NewGuid();
        var session = manager.GetLazySession(taskId);
        await session.GetPageTextAsync(TestContext.Current.CancellationToken);
        var previous = external.LastSession;

        await manager.ReleaseAsync(taskId);

        Assert.True(previous?.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            session.GetPageTextAsync(TestContext.Current.CancellationToken));
        var resumed = manager.GetLazySession(taskId);
        Assert.NotSame(session, resumed);
        await resumed.GetPageTextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, external.CreateCount);
    }

    [Fact]
    public async Task ConcurrentTasks_KeepOwnershipProfilesAndReleaseIndependent()
    {
        var external = new RecordingBrowserSessionFactory();
        var embedded = new RecordingBrowserSessionFactory();
        await using var manager = CreateManager(DefaultBrowserKind.Embedded, external, embedded);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var first = manager.GetLazySession(firstId);
        var second = manager.GetLazySession(secondId);
        Assert.Equal(0, embedded.CreateCount);
        await first.GetPageTextAsync(TestContext.Current.CancellationToken);
        var firstOptions = embedded.LastOptions;
        await second.GetPageTextAsync(TestContext.Current.CancellationToken);
        var secondOptions = embedded.LastOptions;
        var surviving = embedded.LastSession;

        Assert.Equal(firstId, firstOptions?.TaskId);
        Assert.Equal(secondId, secondOptions?.TaskId);
        Assert.NotEqual(firstOptions?.ProfileDirectory, secondOptions?.ProfileDirectory);
        await manager.ReleaseAsync(firstId);
        Assert.False(surviving?.IsDisposed);
        await second.GetPageTextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, embedded.CreateCount);
    }

    private static TaskBrowserSessionManager CreateManager(
        DefaultBrowserKind browser,
        RecordingBrowserSessionFactory external,
        RecordingBrowserSessionFactory embedded) =>
        new(
            external,
            embedded,
            new StubBrowserConfigurationRepository(
                new BrowserConfiguration(
                    browser, BrowserProfilePreference.Managed, null, null, null)),
            new StubConfigurationRepository(),
            new StubBrowserProfilePaths());
}
