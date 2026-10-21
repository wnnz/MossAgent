using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

internal sealed class ProjectAuthorizationAgentScope : IProjectAuthorizationReader, IAsyncDisposable
{
    private ProjectProfile _current;
    private readonly ProjectAuthorizationChanges _changes = new();
    private readonly ProjectAuthorizationMonitor _monitor;
    public ToolExecutionContext Context { get; }

    public ProjectAuthorizationAgentScope()
    {
        _current = new(Guid.NewGuid(), "agent authorization", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow);
        _monitor = new(this, _changes);
        var taskId = Guid.NewGuid();
        Context = new(taskId, _current.PrimaryDirectory, _current.AuthorizedDirectories, ApprovalPolicy.ReadOnly)
        {
            WorkspaceGuard = new ProjectWorkspaceGuard(_monitor, _current, taskId, _current.PrimaryDirectory, _current.AuthorizedDirectories)
        };
    }

    public void Revoke()
    {
        var next = _current with { AuthorizationRevision = 2 };
        Volatile.Write(ref _current, next);
        _changes.Publish(next.Id, next.AuthorizationRevision);
    }

    public Task<ProjectProfile?> ReadAuthorizationAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<ProjectProfile?>(Volatile.Read(ref _current));

    public ValueTask DisposeAsync() => _monitor.DisposeAsync();
}
