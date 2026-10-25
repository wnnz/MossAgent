namespace MossAgent.Application.Attachments;

public sealed record AttachmentReclaimSummary(int Scanned, int Reclaimed, int Retained, int Failed)
{
    public static AttachmentReclaimSummary None { get; } = new(0, 0, 0, 0);

    public bool HasFailures => Failed > 0;
}
