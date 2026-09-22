using System.Net;
using System.Text.Json;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Mcp;

public sealed class McpHttpClientFactory(IConfigurationRepository configurations)
{
    public async Task<HttpClient> CreateAsync(
        McpServerProfile profile,
        CancellationToken cancellationToken)
    {
        var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(20) };
        if (profile.ProxyId is not null)
        {
            var proxies = await configurations.GetProxiesAsync(cancellationToken);
            var selected = proxies.SingleOrDefault(proxy => proxy.Id == profile.ProxyId && proxy.IsEnabled)
                ?? throw new InvalidOperationException("MCP server 选择的代理不存在或已禁用。");
            handler.Proxy = CreateProxy(selected);
            handler.UseProxy = true;
        }

        var client = new HttpClient(handler, true) { Timeout = Timeout.InfiniteTimeSpan };
        foreach (var header in JsonSerializer.Deserialize<Dictionary<string, string>>(profile.HeadersJson) ?? [])
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

