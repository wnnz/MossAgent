using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceSteeringAccess(
    IWorkspaceRepository workspaces, AgentTask task, ProjectProfile? project,
    ToolExecutionContext context, SourceReviewChatAccess? reviewChats, SourceReviewChatBinding? review)
{
    private readonly ProjectProfile? _project = project?.Snapshot();

    public Task ValidateAsync(CancellationToken cancellationToken) => ValidateCoreAsync(cancellationToken);
    public Task ValidateEmptyBoundaryAsync(CancellationToken cancellationToken) => ValidateCoreAsync(cancellationToken);

    private async Task ValidateCoreAsync(CancellationToken cancellationToken)
    {
        if (_project is not null)
        {
            var current = (await workspaces.GetProjectsAsync(cancellationToken)).SingleOrDefault(item => item.Id == _project.Id);
            if (current is null || current.AuthorizationRevision != _project.AuthorizationRevision
                || current.PrimaryDirectory != _project.PrimaryDirectory
                || !current.AuthorizedDirectories.SequenceEqual(_project.AuthorizedDirectories, StringComparer.Ordinal))
                throw new ConversationSteeringException("SteeringScopeChanged");
        }
        if (reviewChats is not null && await reviewChats.ResolveAsync(task, cancellationToken) != review)
            throw new SourceReviewException("source_scope_denied");
        await context.ValidateWorkspaceAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
