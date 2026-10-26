namespace MossAgent.Application.Persistence;

/// <summary>
/// Journal projection of a task-attached browser session. <see cref="Interrupted"/> is
/// written only by startup reconciliation for rows still marked <see cref="Attached"/>
/// after a shutdown; an interrupted session is never re-attached or replayed.
/// </summary>
public enum BrowserSessionState
{
    Attached,
    Detached,
    Disconnected,
    Interrupted
}
