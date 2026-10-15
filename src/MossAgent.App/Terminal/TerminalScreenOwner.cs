using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.Terminal;

internal sealed class TerminalScreenOwner(ToolExecutionContext context, Guid? blockedSession = null, bool blockedLegacy = false) : IDisposable
{
    private readonly string[] _roots = context.AuthorizedRoots.ToArray();
    public Guid ViewId { get; private set; } = Guid.NewGuid();
    public TerminalVtScreen Screen { get; private set; } = new();
    public Guid? SessionId { get; private set; }
    public long StateRevision { get; private set; }
    public bool Failed { get; private set; }
    public long LastVisit { get; set; }
    public Guid? BlockedSession { get; } = blockedSession;

    public bool Matches(ToolExecutionContext candidate) =>
        string.Equals(candidate.WorkingDirectory, context.WorkingDirectory, StringComparison.OrdinalIgnoreCase)
        && candidate.ApprovalPolicy == context.ApprovalPolicy
        && candidate.AuthorizedRoots.SequenceEqual(_roots, StringComparer.OrdinalIgnoreCase);

    public bool ApplyState(TerminalSessionSnapshot snapshot)
    {
        if (snapshot.Revision <= StateRevision || snapshot.SessionId == BlockedSession) return false;
        StateRevision = snapshot.Revision;
        var changed = SessionId != snapshot.SessionId;
        if (changed)
        {
            Screen.Dispose();
            Screen = new();
            SessionId = snapshot.SessionId;
            ViewId = Guid.NewGuid();
            Failed = false;
        }
        var size = snapshot.Grid ?? TerminalGridSize.Default;
        if (Screen.Grid == size) return changed;
        try { Screen.Resize(size); }
        catch (Exception) { Failed = true; }
        return true;
    }

    public bool Append(TerminalOutputEventArgs output, bool requiresSession)
    {
        if (Failed || (blockedLegacy || BlockedSession is not null) && SessionId is null
            || requiresSession && (output.SessionId is null || output.SessionId != SessionId)
            || output.SessionId is not null && output.SessionId != SessionId) return false;
        try { Screen.Process(output.Output); }
        catch (Exception) { Failed = true; }
        return true;
    }

    public void Dispose() => Screen.Dispose();
}
