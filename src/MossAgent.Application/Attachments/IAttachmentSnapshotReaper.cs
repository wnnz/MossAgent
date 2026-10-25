namespace MossAgent.Application.Attachments;

public interface IAttachmentSnapshotReaper
{
    Task<AttachmentReclaimSummary> ReclaimAsync(CancellationToken cancellationToken);
}
