using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class BlockingRootIdentitySession : IRootIdentitySession
{
    private int _disposed;
    public BlockingRootIdentitySession(DirectoryObjectIdentity identity, ManualResetEventSlim gate, Exception? failure = null)
    {
        Roots = [identity];
        Gate = gate;
        Failure = failure;
    }
    public ManualResetEventSlim Gate { get; }
    public Exception? Failure { get; }
    public TaskCompletionSource DisposeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IReadOnlyList<DirectoryObjectIdentity> Roots { get; }
    public bool Completed { get; private set; }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        DisposeStarted.TrySetResult();
        if (!Gate.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("synthetic disposal gate");
        if (Failure is not null) throw Failure;
        Completed = true;
    }
}
