using MossAgent.Domain;

namespace MossAgent.Application.Authorization;

public interface IProjectAuthorizationReader
{
    Task<ProjectProfile?> ReadAuthorizationAsync(Guid projectId, CancellationToken cancellationToken);
}
