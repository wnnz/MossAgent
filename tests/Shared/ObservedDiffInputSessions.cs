using System.Collections.Concurrent;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Testing;

internal sealed class ObservedDiffInputSessions : IDiffInputFileSessionFactory
{
    private readonly IDiffInputFileSessionFactory _native = new WindowsDiffInputFileSessionFactory(new WindowsAuthorizedDirectoryCreator());
    private readonly ConcurrentQueue<ObservedDiffInputSession> _sessions = new();
    private int _created;
    public IReadOnlyList<ObservedDiffInputSession> Sessions => _sessions.ToArray();
    public Action<int>? Creating { get; set; }
    public Action<ObservedDiffInputSession, int>? Created { get; set; }

    public async Task<IDiffInputFileSession> CreateAsync(ToolExecutionContext context, ReadOnlyMemory<byte> content, CancellationToken token)
    {
        var index = Interlocked.Increment(ref _created);
        Creating?.Invoke(index);
        var session = new ObservedDiffInputSession(await _native.CreateAsync(context, content, token));
        _sessions.Enqueue(session);
        try { Created?.Invoke(session, index); return session; }
        catch { session.Dispose(); throw; }
    }
}
