namespace MossAgent.Application.Git;

public enum WorktreeCleanupIntentStage
{
    Recorded = 0,
    GitRemoved = 1,
    Detached = 2,
    Failed = 3,
    Orphaned = 4,
    Acknowledged = 5,
}
