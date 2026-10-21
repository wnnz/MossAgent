using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Testing;

internal sealed class ObservedArtifactSession(IArtifactFileSession inner, Func<CancellationToken, Task> completed) : IArtifactFileSession
{
    public string Path => inner.Path;
    public FileStream Stream => inner.Stream;
    public bool IsComplete { get; private set; }
    public bool IsRetained { get; private set; }

    public async Task CompleteAsync(CancellationToken token)
    {
        await inner.CompleteAsync(token);
        IsComplete = true;
        await completed(token);
    }

    public void Retain() { inner.Retain(); IsRetained = true; }
    public void Dispose() => inner.Dispose();
}
