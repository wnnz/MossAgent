namespace MossAgent.Application.Git;

public enum WorktreeCreationIntentStage
{
    Recorded = 0,
    TargetClaimed = 1,
    GitCreated = 2,
    Failed = 3,
    Consumed = 4,
    Orphaned = 5,
    Acknowledged = 6,
}
