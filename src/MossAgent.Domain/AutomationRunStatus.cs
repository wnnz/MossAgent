namespace MossAgent.Domain;

public enum AutomationRunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled,
    TimedOut,
    Interrupted
}
