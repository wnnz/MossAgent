namespace MossAgent.Infrastructure.Persistence;

internal enum AttachmentReclaimStage
{
    Recorded = 0,
    Reclaimed = 1,
    Failed = 2,
    Retained = 3
}
