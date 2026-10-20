using System.Net;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

[Collection("ProviderHttpClientFactory")]
public sealed class SourceAccountHttpClientFactoryTests
{
    [Fact]
    public async Task FactoryDisablesRedirectsCookiesAndInheritedSystemProxy()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await using var server = new SourceAccountHttpServer(index => index == 0
            ? SourceAccountHttpServer.JsonResponse("{}", "302 Found",
                "Location: http://127.0.0.1:1/must-not-follow\r\nSet-Cookie: credential=must-not-replay\r\n")
            : SourceAccountHttpServer.JsonResponse("{}"), 2, fixture.Token);
        await using var inheritedProxy = new SourceAccountHttpServer(_ => SourceAccountHttpServer.JsonResponse("{}"), 1, fixture.Token);
        var previous = HttpClient.DefaultProxy;
        try
        {
            HttpClient.DefaultProxy = new WebProxy(inheritedProxy.Address) { BypassProxyOnLocal = false };
            var factory = new SourceAccountHttpClientFactory(new SqliteConfigurationRepository(fixture.Connections));
            using var client = await factory.CreateAsync(SourceAccountValidationTests.Create(), fixture.Token);
            Assert.Null(client.DefaultRequestHeaders.Authorization);
            using var redirect = await client.GetAsync(server.Address, fixture.Token);
            Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
            using var second = await client.GetAsync(server.Address, fixture.Token);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            await server.Completion;
            Assert.Equal(2, server.Requests.Count);
            Assert.DoesNotContain(server.Requests.Last(), header => header.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(inheritedProxy.Requests);
        }
        finally { HttpClient.DefaultProxy = previous; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableProxyFailsClosedBeforeClientCreation(bool exists)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var configurations = new SqliteConfigurationRepository(fixture.Connections);
        var id = Guid.NewGuid();
        if (exists) await configurations.SaveProxyAsync(new(id, "disabled", ProxyProtocol.Http, "127.0.0.1",
            23456, null, null, false, false), fixture.Token);
        var exception = await Assert.ThrowsAsync<SourceConnectionException>(() =>
            new SourceAccountHttpClientFactory(configurations).CreateAsync(
                SourceAccountValidationTests.Create(proxyId: id), fixture.Token));
        Assert.Equal(SourceConnectionFailure.ProxyUnavailable, exception.Failure);
    }

    [Fact]
    public async Task HttpsIdentityUsesSelectedProxyWithoutLeakingBearerIntoConnect()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await using var proxyServer = new SourceAccountHttpServer(_ =>
            SourceAccountHttpServer.JsonResponse("{}", "403 Forbidden"), 1, fixture.Token);
        var configurations = new SqliteConfigurationRepository(fixture.Connections);
        var proxy = new ProxyProfile(Guid.NewGuid(), "explicit", ProxyProtocol.Http, "127.0.0.1",
            proxyServer.Address.Port, null, null, false, true);
        await configurations.SaveProxyAsync(proxy, fixture.Token);
        var account = SourceAccountValidationTests.Create(proxyId: proxy.Id);
        var identities = new GitHubIdentityClient(new SourceAccountHttpClientFactory(configurations));
        var exception = await Assert.ThrowsAsync<SourceConnectionException>(() => identities.GetIdentityAsync(account, fixture.Token));
        Assert.Equal(SourceConnectionFailure.TransportFailure, exception.Failure);
        await proxyServer.Completion;
        var request = Assert.Single(proxyServer.Requests);
        Assert.Equal("CONNECT enterprise.example.test:443 HTTP/1.1", request[0]);
        Assert.DoesNotContain(request, header => header.Contains(account.AccessToken, StringComparison.Ordinal));
        Assert.DoesNotContain(account.AccessToken, exception.ToString(), StringComparison.Ordinal);
    }
}
