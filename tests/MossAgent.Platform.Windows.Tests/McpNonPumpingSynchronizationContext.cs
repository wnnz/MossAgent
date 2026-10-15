using System.Collections.Concurrent;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class McpNonPumpingSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
    private int _posts;

    public int Posts => Volatile.Read(ref _posts);

    public override void Post(SendOrPostCallback callback, object? state)
    {
        Interlocked.Increment(ref _posts);
        _callbacks.Enqueue((callback, state));
    }

    public void Drain()
    {
        while (_callbacks.TryDequeue(out var pending)) pending.Callback(pending.State);
    }
}
