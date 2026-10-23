using MossAgent.Domain;

namespace MossAgent.Application.Git;

public interface IGitWorktreeReferenceDiscovery
{
    Task<GitWorktreeReferencePage> SearchAsync(
        ProjectProfile project, string? search, int skip, int take, CancellationToken cancellationToken);
}
