using System.Collections.Concurrent;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Testing;

internal sealed class ObservedArtifactSessions : IArtifactFileSessionFactory
{
    private readonly ConcurrentQueue<ObservedArtifactSession> _created = new();
    private int _count;
    public Action<IArtifactFileSession, int>? Created { get; init; }
    public Action<IArtifactFileSession, int>? Completed { get; init; }
    public Func<IArtifactFileSession, int, CancellationToken, Task>? AsyncCompleted { get; init; }
    public IReadOnlyList<ObservedArtifactSession> Sessions => _created.ToArray();

    public async Task<IArtifactFileSession> CreateAsync(ToolExecutionContext context, string prefix,
        string extension, CancellationToken token)
    {
        var native = await new WindowsArtifactFileSessionFactory(new WindowsAtomicFileWriteSessionFactory(),
            new WindowsAuthorizedDirectoryCreator()).CreateAsync(context, prefix, extension, token);
        var ordinal = Interlocked.Increment(ref _count);
        var session = new ObservedArtifactSession(native, async completionToken =>
        {
            Completed?.Invoke(native, ordinal);
            if (AsyncCompleted is not null) await AsyncCompleted(native, ordinal, completionToken);
        });
        _created.Enqueue(session);
        try { Created?.Invoke(native, ordinal); return session; }
        catch { session.Dispose(); throw; }
    }
}
