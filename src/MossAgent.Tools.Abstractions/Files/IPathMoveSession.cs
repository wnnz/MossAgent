namespace MossAgent.Tools.Abstractions.Files;

/// <summary>A pinned source object; directory moves do not enumerate descendants.</summary>
public interface IPathMoveSession : IDisposable
{
    FileStream? FileContent { get; }
    bool Moved { get; }
    Task<bool> TryCommitAsync(string? expectedDestinationSha256,
        Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken);
}
