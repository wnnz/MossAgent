using System.Net;
using MossAgent.Application.Networking;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Networking;

public sealed class ProviderHttpClientFactory(IConfigurationRepository configurations)
    : IProviderHttpClientFactory
{
    public async Task<HttpClient> CreateAsync(
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        };

        if (provider.ProxyId is not null)
        {
            var proxies = await configurations.GetProxiesAsync(cancellationToken);
            var profile = proxies.SingleOrDefault(proxy => proxy.Id == provider.ProxyId);
            if (profile is null || !profile.IsEnabled)
            {
                throw new InvalidOperationException("供应商选择的代理不存在或已禁用。");
            }

            handler.Proxy = CreateProxy(profile);
            handler.UseProxy = true;
        }

        var baseAddress = new Uri(provider.BaseUri.ToString().TrimEnd('/') + "/");
        var client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = baseAddress,
            Timeout = Timeout.InfiniteTimeSpan
        };
        foreach (var header in provider.Headers)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        return client;
    }

    private static IWebProxy CreateProxy(ProxyProfile profile)
    {
        var proxy = new WebProxy(profile.ToUri());
        if (!string.IsNullOrWhiteSpace(profile.Username))
        {
            proxy.Credentials = new NetworkCredential(profile.Username, profile.Password);
        }

        return proxy;
    }
}
