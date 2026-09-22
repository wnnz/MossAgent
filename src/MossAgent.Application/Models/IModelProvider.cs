using MossAgent.Domain;

namespace MossAgent.Application.Models;

public interface IModelProvider
{
    ProviderProtocol Protocol { get; }

    IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListModelsAsync(
        AiProvider provider,
        CancellationToken cancellationToken);
}

