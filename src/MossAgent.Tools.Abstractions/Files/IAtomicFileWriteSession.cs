namespace MossAgent.Tools.Abstractions.Files;

/// <summary>Owns the staging stream and directory identities until commit or cleanup.</summary>
public interface IAtomicFileWriteSession : IDisposable
{
    string TemporaryPath { get; }
    FileStream Stream { get; }

    /// <returns>False when the target's current content differs from the expected version.</returns>
    Task<bool> TryCommitAsync(string? expectedSha256, CancellationToken cancellationToken);
}
