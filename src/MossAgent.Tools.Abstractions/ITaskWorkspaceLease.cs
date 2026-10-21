namespace MossAgent.Tools.Abstractions;

public interface ITaskWorkspaceLease : IAsyncDisposable
{
    Guid ProjectId { get; }
    long Revision { get; }
    CancellationToken Revocation { get; }
    bool IsRevoked { get; }
}
