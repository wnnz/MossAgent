using System.Runtime.CompilerServices;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class StubModelProvider(
    ProviderProtocol protocol,
    IReadOnlyList<ModelEvent> events,
    TimeSpan delay = default,
    Func<ModelRequest, IReadOnlyList<ModelEvent>>? responseFactory = null) : IModelProvider
{
    public ProviderProtocol Protocol { get; } = protocol;
    public ModelRequest? LastRequest { get; private set; }
    public int Calls { get; private set; }

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LastRequest = request;
        Calls++;
        foreach (var modelEvent in responseFactory?.Invoke(request) ?? events)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            yield return modelEvent;
        }
    }
}
