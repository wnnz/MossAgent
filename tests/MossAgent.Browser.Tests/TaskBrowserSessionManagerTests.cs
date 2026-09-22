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

        var session = manager.GetLazySession(Guid.NewGuid());
        await session.GetPageTextAsync(CancellationToken.None);

        Assert.Equal(0, external.CreateCount);
        Assert.Equal(1, embedded.CreateCount);
        Assert.Equal(BrowserEngine.Embedded, embedded.LastOptions?.Engine);
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
