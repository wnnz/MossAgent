using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public interface IWorkspaceAttachmentReader
{
    Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(
        ProjectProfile project,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken);
}
