using CodingAgent.Domain.Enums;

namespace CodingAgent.Domain.Entities;

/// <summary>网络代理（http/socks5），供 AI 提供商请求使用。</summary>
public class ProxyServer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProxyScheme Scheme { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool Enabled { get; set; } = true;
}
