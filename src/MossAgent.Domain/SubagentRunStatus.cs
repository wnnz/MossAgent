namespace MossAgent.Domain;

public enum SubagentRunStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3,
    Interrupted = 4
}
