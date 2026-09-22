using System.Net;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Infrastructure.Http;
using Xunit;

namespace CodingAgent.Tests;

public class ProxyFactoryTests
{
    private readonly ProxyHttpClientFactory _factory = new();

    [Fact]
    public void BuildProxy_Null_ReturnsNull()
    {
        Assert.Null(ProxyHttpClientFactory.BuildProxy(null));
    }

    [Fact]
    public void BuildProxy_Http_ReturnsHttpProxy()
    {
        var proxy = new ProxyServer { Scheme = ProxyScheme.Http, Host = "127.0.0.1", Port = 8080 };
        var webProxy = ProxyHttpClientFactory.BuildProxy(proxy);
        Assert.NotNull(webProxy);
        var uri = webProxy!.GetProxy(new Uri("https://example.com"));
        Assert.NotNull(uri);
        Assert.Equal("http", uri!.Scheme);
        Assert.Equal(8080, uri.Port);
    }

    [Fact]
    public void BuildProxy_Socks5_ReturnsSocks5Proxy()
    {
        var proxy = new ProxyServer { Scheme = ProxyScheme.Socks5, Host = "127.0.0.1", Port = 1080 };
        var webProxy = ProxyHttpClientFactory.BuildProxy(proxy);
        Assert.NotNull(webProxy);
        var uri = webProxy!.GetProxy(new Uri("https://example.com"));
        Assert.NotNull(uri);
        Assert.Equal(1080, uri!.Port);
        Assert.Contains("socks", uri.Scheme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildProxy_WithCredentials_SetsCredentials()
    {
        var proxy = new ProxyServer
        {
            Scheme = ProxyScheme.Http,
            Host = "proxy.local",
            Port = 8080,
            Username = "user",
            Password = "pass",
        };
        var webProxy = ProxyHttpClientFactory.BuildProxy(proxy);
        Assert.NotNull(webProxy);
        var credentials = Assert.IsType<NetworkCredential>(webProxy!.Credentials);
        Assert.Equal("user", credentials.UserName);
        Assert.Equal("pass", credentials.Password);
    }

    [Fact]
    public void CreateClient_WithoutProxy_ReturnsInfiniteTimeoutClient()
    {
        using var client = _factory.CreateClient(null);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public void CreateClient_WithProxy_UsesProxy()
    {
        var proxy = new ProxyServer { Scheme = ProxyScheme.Socks5, Host = "127.0.0.1", Port = 1080 };
        using var client = _factory.CreateClient(proxy);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }
}
