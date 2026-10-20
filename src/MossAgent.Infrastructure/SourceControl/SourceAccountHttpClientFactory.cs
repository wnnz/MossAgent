using System.Net;
using MossAgent.Application.Persistence;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

public sealed class SourceAccountHttpClientFactory(IConfigurationRepository configurations)
    : ISourceAccountHttpClientFactory
{
    public async Task<HttpClient> CreateAsync(SourceAccount account, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxResponseHeadersLength = 32,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        try
        {
            if (account.ProxyId is { } proxyId)
            {
                var profiles = await configurations.GetProxiesAsync(cancellationToken);
                var profile = profiles.SingleOrDefault(item => item.Id == proxyId && item.IsEnabled);
                if (profile is null) throw new SourceConnectionException(SourceConnectionFailure.ProxyUnavailable);
                var proxy = new WebProxy(profile.ToUri());
                if (!string.IsNullOrWhiteSpace(profile.Username))
                    proxy.Credentials = new NetworkCredential(profile.Username, profile.Password);
                handler.Proxy = proxy;
                handler.UseProxy = true;
            }
            return new HttpClient(handler, disposeHandler: true)
            {
                BaseAddress = account.ApiEndpoint,
                Timeout = Timeout.InfiniteTimeSpan
            };
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }
}
