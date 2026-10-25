using MossAgent.Application.Attachments;

namespace MossAgent.App.Tests;

internal sealed class RecordingAttachmentReaper : IAttachmentSnapshotReaper
{
    public AttachmentReclaimSummary Summary { get; set; } = AttachmentReclaimSummary.None;
    public Exception? Failure { get; set; }
    public int Invocations { get; private set; }

    public Task<AttachmentReclaimSummary> ReclaimAsync(CancellationToken cancellationToken)
    {
        Invocations++;
        if (Failure is not null) throw Failure;
        return Task.FromResult(Summary);
    }
}
