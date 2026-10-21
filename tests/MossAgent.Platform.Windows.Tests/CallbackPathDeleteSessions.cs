using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class CallbackPathDeleteSessions : IPathDeleteSessionFactory
{
    public Action<IPathDeleteSession>? Prepared { get; init; }
    public Action? BeforePrepare { get; init; }
    public Func<IPathDeleteSession, CancellationToken, Task>? Validate { get; init; }
    public int Creates { get; private set; }
    public IPathDeleteSession? Last { get; private set; }

    public IPathDeleteSession Create(string path, IReadOnlyList<string> authorizedRoots,
        bool recursive, bool fileOnly, CancellationToken cancellationToken)
    {
        Creates++;
        BeforePrepare?.Invoke();
        var session = new WindowsPathDeleteSessionFactory().Create(path, authorizedRoots, recursive, fileOnly, cancellationToken);
        Last = session;
        try
        {
            Prepared?.Invoke(session);
            return new CallbackPathDeleteSession(session, Validate);
        }
        catch { session.Dispose(); throw; }
    }
}
