using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Application.Authorization;

public sealed class WorkspaceProjectAuthorizationReader(IWorkspaceRepository workspaces) : IProjectAuthorizationReader
{
    public async Task<ProjectProfile?> ReadAuthorizationAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await workspaces.GetProjectsAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(project => project.Id == projectId);
}
