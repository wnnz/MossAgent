using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class WorktreeNativeRootFactory : ITransferableRootIdentitySessionFactory
{
    private readonly WindowsRootIdentitySessionFactory _native = new();
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _blocked;
    private int _transfers;
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Transfers => Volatile.Read(ref _transfers);

    public void Block(string path) => _blocked = path;
    public void Release() => _released.TrySetResult();

    public Task<IRootIdentitySession> OpenAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken) =>
        OpenNativeAsync(roots, cancellationToken, transferable: false);

    public Task<IRootIdentitySession> OpenTransferableAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken) =>
        OpenNativeAsync(roots, cancellationToken, transferable: true);

    private async Task<IRootIdentitySession> OpenNativeAsync(IReadOnlyList<string> roots,
        CancellationToken cancellationToken, bool transferable)
    {
        if (transferable) Interlocked.Increment(ref _transfers);
        var session = transferable
            ? await _native.OpenTransferableAsync(roots, cancellationToken)
            : await _native.OpenAsync(roots, cancellationToken);
        if (roots.Count == 1 && string.Equals(roots[0], _blocked, StringComparison.OrdinalIgnoreCase))
        {
            Entered.TrySetResult();
            await _released.Task;
        }
        return session;
    }
}
