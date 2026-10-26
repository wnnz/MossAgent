using System.Collections.Concurrent;

namespace MossAgent.Infrastructure.Git;

// Serializes new-run admission (and re-authorization) against cleanup intent
// recording per task. The durable intent row is the cross-process interlock;
// this in-process gate makes the liveness check and the intent insert mutually
// exclusive so an admission cannot slip into the window between them.
internal sealed class WorktreeDrainGates
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    public async Task<SemaphoreSlim> WaitAsync(Guid taskId, CancellationToken token)
    {
        var gate = _gates.GetOrAdd(taskId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token).ConfigureAwait(false);
        return gate;
    }
}
