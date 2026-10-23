using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class PendingRootIdentitySessionFactory(IRootIdentitySessionFactory inner) : IRootIdentitySessionFactory
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _armed;
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Arm() => Interlocked.Exchange(ref _armed, 1);
    public void Release() => _release.TrySetResult();

    public async Task<IRootIdentitySession> OpenAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        var blocked = Interlocked.Exchange(ref _armed, 0) == 1;
        var session = await inner.OpenAsync(roots, cancellationToken);
        if (!blocked) return session;
        Entered.TrySetResult();
        // Deliberately delay delivery after a real native open to simulate an uncooperative OS call.
        await _release.Task;
        return new ObservedRootIdentitySession(session, () => Disposed.TrySetResult());
    }
}
