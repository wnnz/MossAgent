using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

public sealed class DeferredModelDiscoveryService : IModelDiscoveryService
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<IReadOnlyList<DiscoveredModel>> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;
    public AiProvider? RequestedProvider { get; private set; }

    public async Task<IReadOnlyList<DiscoveredModel>> DiscoverAsync(
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        RequestedProvider = provider;
        _started.TrySetResult();
        return await _result.Task.WaitAsync(cancellationToken);
    }

    public void Complete(IReadOnlyList<DiscoveredModel> models) => _result.TrySetResult(models);

    public void Fail(Exception exception) => _result.TrySetException(exception);
}
