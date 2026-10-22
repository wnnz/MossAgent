using MossAgent.Domain;

namespace MossAgent.Application.Git;

public interface IGitReferenceDiscoveryService
{
    Task<GitReferencePage> ListLocalAsync(
        ProjectProfile project,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GitReferencePreview> PreviewAsync(
        ProjectProfile project,
        string reference,
        CancellationToken cancellationToken);
}
