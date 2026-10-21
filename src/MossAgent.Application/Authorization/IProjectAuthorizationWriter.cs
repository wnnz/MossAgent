using MossAgent.Domain;

namespace MossAgent.Application.Authorization;

public interface IProjectAuthorizationWriter
{
    // Return this transaction's committed snapshot, never a later project's revision.
    Task<ProjectProfile> SaveAuthorizationAsync(ProjectProfile project, CancellationToken cancellationToken);
}
