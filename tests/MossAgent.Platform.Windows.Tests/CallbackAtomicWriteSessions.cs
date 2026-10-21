using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class CallbackAtomicWriteSessions(Action<IAtomicFileWriteSession>? afterCreate = null) : IAtomicFileWriteSessionFactory
{
    public int Creates { get; private set; }

    public IAtomicFileWriteSession Create(string path, IReadOnlyList<string> roots, CancellationToken token)
    {
        var session = new WindowsAtomicFileWriteSessionFactory().Create(path, roots, token);
        Creates++;
        try { afterCreate?.Invoke(session); return session; }
        catch { session.Dispose(); throw; }
    }
}
