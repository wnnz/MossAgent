namespace MossAgent.Tools.Abstractions.Files;

/// <summary>
/// A pinned directory that can be atomically moved and then deleted without resolving
/// the original path again.
/// </summary>
public interface IPathQuarantineSession : IDisposable
{
    bool Moved { get; }

    Task MoveAsync(Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken);

    Task DeleteMovedAsync(Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken);
}
