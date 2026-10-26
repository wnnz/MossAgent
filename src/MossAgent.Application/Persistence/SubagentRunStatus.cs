namespace MossAgent.Application.Persistence;

/// <summary>
/// Journal projection of a delegated subagent run. <see cref="Interrupted"/> is written only
/// by startup reconciliation for rows still marked <see cref="Running"/> after a shutdown;
/// an interrupted run is never replayed.
/// </summary>
public enum SubagentRunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled,
    Interrupted
}
