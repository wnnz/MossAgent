using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Git;

public sealed class WorktreeRootLease(Guid projectId, long revision, IRootIdentitySession? session,
    ITaskWorkspaceLease? owner = null) : ITaskWorkspaceLease
{
    private readonly Lock _sync = new();
    private Task? _disposal;
    public Guid ProjectId => projectId;
    public long Revision => revision;
    public CancellationToken Revocation => owner?.Revocation ?? CancellationToken.None;
    public bool IsRevoked => owner?.IsRevoked ?? false;

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_sync)
        {
            if (_disposal is not null) return new(_disposal);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposal = completion.Task;
        }
        _ = DisposeOwnedAsync(completion);
        return new(_disposal);
    }

    private async Task DisposeOwnedAsync(TaskCompletionSource completion)
    {
        try
        {
            try { session?.Dispose(); }
            finally { if (owner is not null) await owner.DisposeAsync().ConfigureAwait(false); }
            completion.SetResult();
        }
        catch (Exception exception) { completion.SetException(exception); }
    }
}
