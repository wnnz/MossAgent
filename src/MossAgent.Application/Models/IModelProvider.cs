using MossAgent.Domain;

namespace MossAgent.Application.Models;

public interface IModelProvider
{
    ProviderProtocol Protocol { get; }

    IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        CancellationToken cancellationToken);
}
