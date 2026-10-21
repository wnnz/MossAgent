using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class ProjectAuthorizationPtyFixture : IProjectAuthorizationReader, IAsyncDisposable
{
    private ProjectProfile _current;
    public ProjectProfile Project { get; }
    public ProjectAuthorizationChanges Changes { get; } = new();
    public ProjectAuthorizationMonitor Monitor { get; }

    public ProjectAuthorizationPtyFixture(string root)
    {
        Project = new(Guid.NewGuid(), "terminal authorization", root, [root], DateTimeOffset.UtcNow);
        _current = Project;
        Monitor = new(this, Changes);
    }

    public ToolExecutionContext Context(Guid taskId) => new(taskId, Project.PrimaryDirectory, Project.AuthorizedDirectories, ApprovalPolicy.FullAccess)
    {
        WorkspaceGuard = new ProjectWorkspaceGuard(Monitor, Project, taskId, Project.PrimaryDirectory, Project.AuthorizedDirectories)
    };

    public Task<ProjectProfile?> ReadAuthorizationAsync(Guid projectId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ProjectProfile?>(Volatile.Read(ref _current));
    }

    public void Revoke()
    {
        var next = Project with { AuthorizationRevision = 2, AuthorizedDirectories = [] };
        Volatile.Write(ref _current, next);
        Changes.Publish(Project.Id, 2);
    }

    public ValueTask DisposeAsync() => Monitor.DisposeAsync();
}
