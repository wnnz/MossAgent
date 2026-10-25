namespace MossAgent.Tools.Abstractions.Files;

/// <summary>Journaled lifecycle of one created artifact or diff-input file session.</summary>
public enum ArtifactSessionStage
{
    Recorded = 0,
    Completed = 1,
    Retained = 2,
    Discarded = 3,
    CleanupUnknown = 4,
    Orphaned = 5,
    Acknowledged = 6,
}
