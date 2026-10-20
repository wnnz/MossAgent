using System.Runtime.CompilerServices;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class SteeringProbeModelProvider : IModelProvider
{
    public ProviderProtocol Protocol => ProviderProtocol.OpenAiResponses;
    public List<ModelRequest> Requests { get; } = [];
    public Action<int>? DuringStream { get; set; }
    public bool FirstTurnTools { get; set; }

    public async IAsyncEnumerable<ModelEvent> StreamAsync(ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        yield return new TextDelta($"answer {Requests.Count}");
        DuringStream?.Invoke(Requests.Count);
        if (FirstTurnTools && Requests.Count == 1)
        {
            yield return new ToolCallCompleted("first-call", "test.read", JsonSerializer.SerializeToElement(new { }));
            yield return new ToolCallCompleted("second-call", "test.read", JsonSerializer.SerializeToElement(new { }));
        }
        await Task.CompletedTask;
        yield return new ModelCompleted();
    }
}
