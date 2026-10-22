using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Git;

public interface IGitWorktreeAuthorizationService
{
    Task<WorktreeRootAuthorization?> ReadAuthorizationAsync(Guid taskId, CancellationToken token);
    Task<ITaskWorkspaceLease> AcquireAsync(ProjectProfile project, Guid taskId, string path, CancellationToken token);
    Task ConfirmLegacyAsync(ProjectProfile project, AgentTask task, CancellationToken token);
    Task<WorktreeRootReauthorization> ReauthorizeAsync(
        ProjectProfile project, AgentTask task, CancellationToken token);
}
