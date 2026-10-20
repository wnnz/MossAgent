namespace MossAgent.Application.Attachments;

public interface ISelectedAttachmentReader
{
    Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadSelectedAsync(
        IReadOnlyList<string> selectedAbsolutePaths, CancellationToken cancellationToken);
}
