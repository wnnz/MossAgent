using System.Net;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;

namespace CodingAgent.Infrastructure.Http;

/// <summary>按代理配置构建 HttpClient（http / socks5，.NET 内建 SOCKS 支持）。SSE 流式需禁用整体超时。</summary>
public class ProxyHttpClientFactory
{
    public HttpClient CreateClient(ProxyServer? proxy)
    {
        var handler = new SocketsHttpHandler
        {
            // SSE 流式响应：禁用整体超时，由调用方用 CancellationToken 控制
            ConnectTimeout = TimeSpan.FromSeconds(30),
        };

        var webProxy = BuildProxy(proxy);
        if (webProxy is not null)
        {
            handler.Proxy = webProxy;
            handler.UseProxy = true;
        }

        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary>按代理配置构建 IWebProxy（http / socks5，.NET 内建 SOCKS 支持）。</summary>
    public static IWebProxy? BuildProxy(ProxyServer? proxy)
    {
        if (proxy is null)
        {
            return null;
        }
        var scheme = proxy.Scheme == ProxyScheme.Socks5 ? "socks5" : "http";
        var webProxy = new WebProxy(new Uri($"{scheme}://{proxy.Host}:{proxy.Port}"));
        if (!string.IsNullOrEmpty(proxy.Username))
        {
            webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password ?? string.Empty);
        }
        return webProxy;
    }
}
