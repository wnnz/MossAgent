using System.Runtime.CompilerServices;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class ProjectAuthorizationStreamProvider : IModelProvider
{
    public ProviderProtocol Protocol => ProviderProtocol.OpenAiResponses;
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Cancelled { get; private set; }

    public async IAsyncEnumerable<ModelEvent> StreamAsync(ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            yield return new TextDelta("retained prefix");
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        finally { Cancelled = cancellationToken.IsCancellationRequested; }
    }
}
