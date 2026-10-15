using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProxyDeletionSafetyTests
{
    [Theory]
    [InlineData("provider")]
    [InlineData("disabled-provider")]
    [InlineData("browser")]
    [InlineData("mcp")]
    [InlineData("disabled-mcp")]
    public Task ReferencedProxy_CannotBeDeletedOrSilentlyBecomeDirect(string owner) => RunAsync(async connections =>
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configurations = new SqliteConfigurationRepository(connections);
        var browsers = new SqliteBrowserConfigurationRepository(connections);
        var mcp = new SqliteMcpConfigurationRepository(connections);
        var proxy = CreateProxy();
        await configurations.SaveProxyAsync(proxy, cancellationToken);
        if (owner.Contains("provider", StringComparison.Ordinal))
            await configurations.SaveProviderAsync(new AiProvider(Guid.NewGuid(), "provider", ProviderProtocol.OpenAiResponses,
                new Uri("https://example.test/v1/"), "fixture-key", proxy.Id, false, owner == "provider",
                new Dictionary<string, string>()), cancellationToken);
        else if (owner == "browser")
            await browsers.SaveAsync(new BrowserConfiguration(DefaultBrowserKind.Embedded,
                BrowserProfilePreference.Managed, null, null, proxy.Id), cancellationToken);
        else
            await mcp.SaveAsync(new McpServerProfile(Guid.NewGuid(), "mcp", McpTransportKind.StreamableHttp,
                null, "[]", null, "{}", new Uri("https://example.test/mcp"), "{}", proxy.Id,
                owner == "mcp"), cancellationToken);

        await Assert.ThrowsAsync<ProxyInUseException>(() => configurations.DeleteProxyAsync(proxy.Id, cancellationToken));

        Assert.Equal(proxy, Assert.Single(await configurations.GetProxiesAsync(cancellationToken)));
        if (owner.Contains("provider", StringComparison.Ordinal))
            Assert.Equal(proxy.Id, Assert.Single(await configurations.GetProvidersAsync(cancellationToken)).ProxyId);
        else if (owner == "browser")
            Assert.Equal(proxy.Id, (await browsers.GetAsync(cancellationToken)).ProxyId);
        else Assert.Equal(proxy.Id, Assert.Single(await mcp.GetAllAsync(cancellationToken)).ProxyId);
    });

    [Fact]
    public Task UnreferencedProxyAndMissingIdentity_CanBeDeleted() => RunAsync(async connections =>
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configurations = new SqliteConfigurationRepository(connections);
        var proxy = CreateProxy();
        await configurations.SaveProxyAsync(proxy, cancellationToken);

        await configurations.DeleteProxyAsync(proxy.Id, cancellationToken);
        await configurations.DeleteProxyAsync(proxy.Id, cancellationToken);

        Assert.Empty(await configurations.GetProxiesAsync(cancellationToken));
    });

    [Fact]
    public Task RemovingTheReference_AllowsDeletionWithoutChangingOtherProviders() => RunAsync(async connections =>
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configurations = new SqliteConfigurationRepository(connections);
        var proxy = CreateProxy();
        await configurations.SaveProxyAsync(proxy, cancellationToken);
        var provider = new AiProvider(Guid.NewGuid(), "provider", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1/"), "fixture-key", proxy.Id, false, true, new Dictionary<string, string>());
        await configurations.SaveProviderAsync(provider, cancellationToken);
        await Assert.ThrowsAsync<ProxyInUseException>(() => configurations.DeleteProxyAsync(proxy.Id, cancellationToken));

        await configurations.SaveProviderAsync(provider with { ProxyId = null }, cancellationToken);
        await configurations.DeleteProxyAsync(proxy.Id, cancellationToken);

        Assert.Empty(await configurations.GetProxiesAsync(cancellationToken));
        Assert.Null(Assert.Single(await configurations.GetProvidersAsync(cancellationToken)).ProxyId);
    });

    private static ProxyProfile CreateProxy() => new(Guid.NewGuid(), "proxy", ProxyProtocol.Http,
        "127.0.0.1", 8080, "fixture-user", "fixture-password", true, true);

    private static async Task RunAsync(Func<SqliteConnectionFactory, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(TestContext.Current.CancellationToken);
            await action(connections);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
