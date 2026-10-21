using MossAgent.Domain;

namespace MossAgent.Application.Authorization;

public interface IProjectAuthorizationConfirmation
{
    bool CanConfirmRootObjects { get; }
    Task<ProjectProfile> ConfirmRootObjectsAsync(ProjectProfile expected, CancellationToken cancellationToken);
}
