using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class CallbackPathMoveSessions : IPathMoveSessionFactory
{
    public Action? BeforePrepare { get; init; }
    public Action? AfterPrepare { get; init; }
    public IPathMoveSession? Last { get; private set; }
    public int Creates { get; private set; }

    public IPathMoveSession Create(string source, string destination, IReadOnlyList<string> roots, CancellationToken token)
    {
        Creates++;
        BeforePrepare?.Invoke();
        var session = new WindowsPathMoveSessionFactory().Create(source, destination, roots, token);
        try { AfterPrepare?.Invoke(); return Last = session; }
        catch { session.Dispose(); throw; }
    }
}
