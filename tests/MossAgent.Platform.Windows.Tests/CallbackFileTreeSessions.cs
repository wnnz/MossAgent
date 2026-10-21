using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class CallbackFileTreeSessions : IFileTreeReadSessionFactory
{
    public Action? BeforePrepare { get; init; }
    public Action<IFileTreeReadSession>? Prepared { get; init; }
    public int Creates { get; private set; }
    public IFileTreeReadSession? Last { get; private set; }

    public IFileTreeReadSession Create(string path, IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken)
    {
        Creates++;
        BeforePrepare?.Invoke();
        var session = new WindowsFileTreeReadSessionFactory().Create(path, authorizedRoots, cancellationToken);
        Last = session;
        try { Prepared?.Invoke(session); return session; }
        catch { session.Dispose(); throw; }
    }
}
