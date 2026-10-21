namespace MossAgent.Tools.Abstractions.Files;

public interface IPathDeleteSession : IDisposable
{
    Stream? FileContent { get; }
    int PlannedCount { get; }
    // Requests accepted by the OS, which may still be pending other open readers.
    int DeletedCount { get; }
    Task DeleteAsync(Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken);
}
