using MossAgent.Domain;

namespace MossAgent.Application.Networking;

public interface IProviderHttpClientFactory
{
    Task<HttpClient> CreateAsync(AiProvider provider, CancellationToken cancellationToken);
}

