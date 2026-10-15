namespace MossAgent.Domain;

public sealed record ProxyProfile(
    Guid Id,
    string Name,
    ProxyProtocol Protocol,
    string Host,
    int Port,
    string? Username,
    string? Password,
    bool IsDefault,
    bool IsEnabled)
{
    public override string ToString() => Name;

    public Uri ToUri() => TryGetUri(out var uri)
        ? uri!
        : throw new UriFormatException("代理主机名或端口无效。");

    public bool TryGetUri(out Uri? uri)
    {
        var host = Host.Trim();
        if (host.Length >= 2 && host[0] == '[' && host[^1] == ']')
        {
            host = host[1..^1];
        }

        var scheme = Protocol switch
        {
            ProxyProtocol.Http => Uri.UriSchemeHttp,
            ProxyProtocol.Https => Uri.UriSchemeHttps,
            ProxyProtocol.Socks5 => "socks5",
            _ => null
        };
        if (scheme is null || Port is < 1 or > 65535
            || Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            uri = null;
            return false;
        }

        try
        {
            uri = new UriBuilder(scheme, host, Port).Uri;
            return true;
        }
        catch (UriFormatException)
        {
            uri = null;
            return false;
        }
    }
}
