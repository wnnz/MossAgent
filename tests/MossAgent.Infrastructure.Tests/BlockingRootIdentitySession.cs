using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class BlockingRootIdentitySession : IRootIdentitySession
{
    private int _attempts;
    public BlockingRootIdentitySession(DirectoryObjectIdentity identity, ManualResetEventSlim gate,
        Exception? failure = null, int failAttempts = int.MaxValue)
    {
        Roots = [identity];
        Gate = gate;
        Failure = failure;
        FailAttempts = failAttempts;
    }
    public ManualResetEventSlim Gate { get; }
    public Exception? Failure { get; }
    public int FailAttempts { get; }
    public int Attempts => Volatile.Read(ref _attempts);
    public TaskCompletionSource DisposeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IReadOnlyList<DirectoryObjectIdentity> Roots { get; }
    public bool Completed { get; private set; }
    public void Dispose()
    {
        var attempt = Interlocked.Increment(ref _attempts);
        DisposeStarted.TrySetResult();
        if (!Gate.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("synthetic disposal gate");
        if (Failure is not null && attempt <= FailAttempts) throw Failure;
        Completed = true;
    }
}
