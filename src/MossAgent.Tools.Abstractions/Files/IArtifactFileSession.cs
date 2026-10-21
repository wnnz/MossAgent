namespace MossAgent.Tools.Abstractions.Files;

/// <summary>Owns a new artifact until the caller explicitly retains it.</summary>
public interface IArtifactFileSession : IDisposable
{
    string Path { get; }
    FileStream Stream { get; }
    /// <summary>Finishes a new file while retaining exclusive ownership and rollback responsibility.</summary>
    Task CompleteAsync(CancellationToken cancellationToken);
    /// <summary>Confirms the caller's success after completion. Disposal then only releases handles.</summary>
    void Retain();
}
