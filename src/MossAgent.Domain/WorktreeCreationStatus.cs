namespace MossAgent.Domain;

public enum WorktreeCreationStatus
{
    Preparing = 0,
    Claimed = 1,
    GitCreated = 2,
    Failed = 3,
    Consumed = 4,
    Orphaned = 5,
    Acknowledged = 6
}
