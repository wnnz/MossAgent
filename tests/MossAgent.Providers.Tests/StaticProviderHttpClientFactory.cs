using MossAgent.Application.Networking;
using MossAgent.Domain;

namespace MossAgent.Providers.Tests;

public sealed class StaticProviderHttpClientFactory(HttpMessageHandler handler)
    : IProviderHttpClientFactory
{
    public Task<HttpClient> CreateAsync(
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var baseAddress = new Uri(provider.BaseUri.ToString().TrimEnd('/') + "/");
        return Task.FromResult(new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = baseAddress
        });
    }
}
