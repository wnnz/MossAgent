namespace MossAgent.Domain;

public enum AgentTaskStatus
{
    Draft,
    Running,
    WaitingForApproval,
    Completed,
    Failed,
    Cancelled,
    Interrupted
}

