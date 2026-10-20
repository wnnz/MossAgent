using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface ISourceAccountHttpClientFactory
{
    Task<HttpClient> CreateAsync(SourceAccount account, CancellationToken cancellationToken);
}
