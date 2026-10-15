using Microsoft.Extensions.DependencyInjection;
using MossAgent.Application.Artifacts;
using MossAgent.Application.Persistence;
using MossAgent.App.ViewModels;
using MossAgent.Browser;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BrowserSettingsRuntimeFlowTests
{
    [Theory]
    [InlineData(DefaultBrowserKind.Embedded, BrowserEngine.Embedded)]
    [InlineData(DefaultBrowserKind.Chrome, BrowserEngine.Chrome)]
    [InlineData(DefaultBrowserKind.Edge, BrowserEngine.Edge)]
    public async Task SavedBrowserChoice_RoutesNewTaskToMatchingSessionFactory(
        DefaultBrowserKind selectedBrowser, BrowserEngine expectedEngine)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
            var settings = services.GetRequiredService<BrowserSettingsViewModel>();
            await settings.Initialization;
            await settings.RefreshCommand.ExecuteAsync(null);
            settings.DefaultBrowser = selectedBrowser;
            await settings.SaveCommand.ExecuteAsync(null);
            Assert.Contains("已保存", settings.Status);

            var external = new RejectingBrowserSessionFactory();
            var embedded = new RejectingBrowserSessionFactory();
            await using var manager = new TaskBrowserSessionManager(
                external, embedded,
                services.GetRequiredService<IBrowserConfigurationRepository>(),
                services.GetRequiredService<IConfigurationRepository>(),
                services.GetRequiredService<IBrowserProfilePaths>());
            var session = manager.GetLazySession(Guid.NewGuid());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                session.GetPageTextAsync(cancellationToken));

            var selectedFactory = selectedBrowser == DefaultBrowserKind.Embedded ? embedded : external;
            var otherFactory = selectedBrowser == DefaultBrowserKind.Embedded ? external : embedded;
            Assert.Equal(1, selectedFactory.CreateCount);
            Assert.Equal(0, otherFactory.CreateCount);
            Assert.Equal(expectedEngine, selectedFactory.LastOptions?.Engine);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
