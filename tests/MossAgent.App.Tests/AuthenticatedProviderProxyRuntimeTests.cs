using Microsoft.Extensions.DependencyInjection;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AuthenticatedProviderProxyRuntimeTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Socks5)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Socks5)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Socks5)]
    public async Task SavedCredentialsAuthenticateDiscoveryAndStreaming(ProviderProtocol protocol, ProxyProtocol proxyProtocol)
    {
        using var timeout = Timeout();
        await using var server = Server(protocol, proxyProtocol, timeout.Token);
        await using var fixture = await AuthenticatedProxySettingsFixture.CreateAsync(
            proxyProtocol, server.Port, false, false, timeout.Token);
        var provider = await fixture.SaveProviderAsync(protocol, "http://models.example.test/v1/", false, timeout.Token);

        var models = await fixture.Services.GetRequiredService<IModelDiscoveryService>().DiscoverAsync(provider, timeout.Token);
        var events = await fixture.StreamAsync(provider, timeout.Token);

        Assert.Equal("fixture-model", Assert.Single(models).Id);
        Assert.Contains(events, item => item is TextDelta { Text: "hello" });
        Assert.Contains(events, item => item is ModelCompleted);
        Assert.Equal(2, server.Requests.Count(item => item.Authenticated));
        Assert.DoesNotContain(server.Requests, item => item.CredentialLeak);
        if (proxyProtocol == ProxyProtocol.Socks5)
            Assert.DoesNotContain(server.Requests, item => item.ProxyAuthorization);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Http, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Http, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Http, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Socks5, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Socks5, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Socks5, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Http, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Http, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Http, true)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Socks5, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Socks5, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Socks5, true)]
    public async Task RejectedCredentialsNeverLeakOrFallbackToDirect(
        ProviderProtocol protocol, ProxyProtocol proxyProtocol, bool missing)
    {
        using var timeout = Timeout();
        await using var proxy = Server(protocol, proxyProtocol, timeout.Token);
        await using var direct = Server(protocol, null, timeout.Token);
        await using var fixture = await AuthenticatedProxySettingsFixture.CreateAsync(
            proxyProtocol, proxy.Port, missing, true, timeout.Token);
        var provider = await fixture.SaveProviderAsync(protocol, $"http://127.0.0.1:{direct.Port}/v1/", false, timeout.Token);

        await AssertRejectedAsync(fixture, provider, proxyProtocol, timeout.Token);
        Assert.True(proxy.Rejections >= 2);
        Assert.DoesNotContain(proxy.Requests, item => item.Authenticated);
        Assert.Equal(0, direct.Connections);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Socks5)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Socks5)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Socks5)]
    public async Task ExplicitDirectDoesNotUseDefaultProxyOrTransmitItsCredentials(
        ProviderProtocol protocol, ProxyProtocol proxyProtocol)
    {
        using var timeout = Timeout();
        await using var proxy = Server(protocol, proxyProtocol, timeout.Token);
        await using var direct = Server(protocol, null, timeout.Token);
        await using var fixture = await AuthenticatedProxySettingsFixture.CreateAsync(
            proxyProtocol, proxy.Port, false, false, timeout.Token);
        var provider = await fixture.SaveProviderAsync(protocol, $"http://127.0.0.1:{direct.Port}/v1/", true, timeout.Token);

        await fixture.Services.GetRequiredService<IModelDiscoveryService>().DiscoverAsync(provider, timeout.Token);
        var events = await fixture.StreamAsync(provider, timeout.Token);

        Assert.Contains(events, item => item is ModelCompleted);
        Assert.Equal(0, proxy.Connections);
        Assert.Equal(2, direct.Requests.Count);
        Assert.DoesNotContain(direct.Requests, item => item.ProxyAuthorization || item.CredentialLeak);
        Assert.True(fixture.Window.Proxies.Items.Single().IsDefault);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Http)]
    [InlineData(ProviderProtocol.OpenAiResponses, ProxyProtocol.Socks5)]
    [InlineData(ProviderProtocol.OpenAiCompatible, ProxyProtocol.Socks5)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, ProxyProtocol.Socks5)]
    public async Task EditingCredentialsTakesEffectAndRestoringThemRecoversRequests(
        ProviderProtocol protocol, ProxyProtocol proxyProtocol)
    {
        using var timeout = Timeout();
        await using var direct = Server(protocol, null, timeout.Token);
        await using var proxy = Server(protocol, proxyProtocol, timeout.Token, "127.0.0.1", direct.Port);
        await using var fixture = await AuthenticatedProxySettingsFixture.CreateAsync(
            proxyProtocol, proxy.Port, false, false, timeout.Token);
        var provider = await fixture.SaveProviderAsync(protocol, $"http://127.0.0.1:{direct.Port}/v1/", false, timeout.Token);
        var discovery = fixture.Services.GetRequiredService<IModelDiscoveryService>();
        await discovery.DiscoverAsync(provider, timeout.Token);
        Assert.Contains(await fixture.StreamAsync(provider, timeout.Token), item => item is ModelCompleted);

        await fixture.ChangePasswordAsync(true, timeout.Token);
        await AssertRejectedAsync(fixture, provider, proxyProtocol, timeout.Token);
        await fixture.ChangePasswordAsync(false, timeout.Token);

        Assert.False(fixture.Window.Proxies.HasStatusError);
        Assert.Equal("fixture-model", Assert.Single(await discovery.DiscoverAsync(provider, timeout.Token)).Id);
        Assert.Contains(await fixture.StreamAsync(provider, timeout.Token), item => item is ModelCompleted);
        Assert.Equal(4, proxy.Requests.Count(item => item.Authenticated));
        Assert.True(proxy.Rejections >= 2);
        Assert.DoesNotContain(proxy.Requests, item => item.CredentialLeak);
        Assert.Equal(0, direct.Connections);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true)]
    public async Task SettingsDiscovery_ExplainsRejectedProxyAuthentication(ProviderProtocol protocol, bool missing)
    {
        using var timeout = Timeout();
        await using var direct = Server(protocol, null, timeout.Token);
        await using var proxy = Server(protocol, ProxyProtocol.Http, timeout.Token, "127.0.0.1", direct.Port);
        await using var fixture = await AuthenticatedProxySettingsFixture.CreateAsync(
            ProxyProtocol.Http, proxy.Port, missing, true, timeout.Token);
        await fixture.SaveProviderAsync(protocol, $"http://127.0.0.1:{direct.Port}/v1/", false, timeout.Token);

        await fixture.Window.Models.FetchModelsForProviderCommand.ExecuteAsync(
            Assert.Single(fixture.Window.Models.ProviderGroups)).WaitAsync(timeout.Token);

        Assert.True(fixture.Window.Models.HasStatusError);
        Assert.Contains("HTTP 407", fixture.Window.Models.Status);
        Assert.Contains("代理认证失败", fixture.Window.Models.Status);
        Assert.Contains("用户名", fixture.Window.Models.Status);
        Assert.Contains("密码", fixture.Window.Models.Status);
        AssertNoCredentials(fixture.Window.Models.Status);
        Assert.True(proxy.Rejections > 0);
        Assert.Equal(0, direct.Connections);
        Assert.DoesNotContain(proxy.Requests, request => request.CredentialLeak);
    }

    private static async Task AssertRejectedAsync(AuthenticatedProxySettingsFixture fixture,
        AiProvider provider, ProxyProtocol proxyProtocol, CancellationToken token)
    {
        var discoveryFailure = await Assert.ThrowsAsync<HttpRequestException>(() =>
            fixture.Services.GetRequiredService<IModelDiscoveryService>().DiscoverAsync(provider, token));
        AssertNoCredentials(discoveryFailure.Message);
        if (proxyProtocol == ProxyProtocol.Http)
        {
            var events = await fixture.StreamAsync(provider, token);
            Assert.True(events.Count == 1 && events[0] is ModelFailed);
            AssertNoCredentials(((ModelFailed)events[0]).Message);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.StreamAsync(provider, token));
            AssertNoCredentials(failure.Message);
        }
    }

    private static void AssertNoCredentials(string value)
    {
        Assert.False(value.Contains(AuthenticatedProxyTestServer.Username, StringComparison.Ordinal));
        Assert.False(value.Contains(AuthenticatedProxyTestServer.Password, StringComparison.Ordinal));
        Assert.False(value.Contains("wrong-fixture-secret", StringComparison.Ordinal));
    }

    private static AuthenticatedProxyTestServer Server(ProviderProtocol protocol, ProxyProtocol? proxyProtocol,
        CancellationToken token, string expectedHost = "models.example.test", int expectedPort = 80)
        => new(proxyProtocol, path => path == "/v1/models"
            ? ProviderRoutingTestProtocol.Models(protocol) : ProviderRoutingTestProtocol.Stream(protocol),
            token, expectedHost, expectedPort);

    private static CancellationTokenSource Timeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        return timeout;
    }
}
