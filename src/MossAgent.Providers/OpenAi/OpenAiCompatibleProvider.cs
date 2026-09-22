using System.Runtime.CompilerServices;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Providers.OpenAi;

public sealed class OpenAiCompatibleProvider(OpenAiResponsesProvider inner) : IModelProvider
{
    public ProviderProtocol Protocol => ProviderProtocol.OpenAiCompatible;

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var modelEvent in inner.StreamAsync(request, cancellationToken))
        {
            yield return modelEvent;
        }
    }

    public Task<IReadOnlyList<string>> ListModelsAsync(
        AiProvider provider,
        CancellationToken cancellationToken) => inner.ListModelsAsync(provider, cancellationToken);
}

