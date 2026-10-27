using MossAgent.Application.Authorization;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Tests;

internal sealed class MultiProjectAuthorizationReaderProbe : IProjectAuthorizationReader
{
    private readonly Dictionary<Guid, ProjectProfile> _projects = [];

    public void Set(ProjectProfile project)
    {
        lock (_projects) _projects[project.Id] = project;
    }

    public Task<ProjectProfile?> ReadAuthorizationAsync(Guid projectId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_projects) return Task.FromResult(_projects.GetValueOrDefault(projectId));
    }
}
