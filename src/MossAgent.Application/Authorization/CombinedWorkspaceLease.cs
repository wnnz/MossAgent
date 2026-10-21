using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Authorization;

internal sealed class CombinedWorkspaceLease : ITaskWorkspaceLease
{
    private readonly ITaskWorkspaceLease _project;
    private readonly ITaskWorkspaceLease _environment;
    private readonly Lock _sync = new();
    private readonly CancellationTokenSource _revocation;
    private readonly CancellationToken _revocationToken;
    private Task? _disposal;
    public CombinedWorkspaceLease(ITaskWorkspaceLease project, ITaskWorkspaceLease environment)
    {
        _project = project;
        _environment = environment;
        _revocation = CancellationTokenSource.CreateLinkedTokenSource(project.Revocation, environment.Revocation);
        _revocationToken = _revocation.Token;
    }
    public Guid ProjectId => _project.ProjectId;
    public long Revision => _project.Revision;
    public CancellationToken Revocation => _revocationToken;
    public bool IsRevoked => _project.IsRevoked || _environment.IsRevoked;

    public ValueTask DisposeAsync()
    {
        lock (_sync) return new(_disposal ??= DisposeOwnedAsync());
    }

    private async Task DisposeOwnedAsync()
    {
        try { await _environment.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            try { await _project.DisposeAsync().ConfigureAwait(false); }
            finally { _revocation.Dispose(); }
        }
    }
}
