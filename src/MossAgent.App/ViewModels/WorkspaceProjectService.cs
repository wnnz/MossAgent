using MossAgent.Application.Persistence;
using MossAgent.App.Services;
using MossAgent.Domain;
using MossAgent.Application.Authorization;
using MossAgent.Application.Git;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceProjectService(
    IWorkspaceRepository workspaces,
    IFolderPickerService folderPicker,
    ProjectAuthorizationMonitor? authorizations = null,
    TimeSpan? authorizationDrainTimeout = null,
    TimeSpan? authorizationStaleAfter = null,
    IGitWorktreeService? worktrees = null)
{
    private readonly TimeSpan _authorizationDrainTimeout =
        authorizationDrainTimeout ?? TimeSpan.FromSeconds(5);
    private readonly TimeSpan _authorizationStaleAfter =
        authorizationStaleAfter ?? TimeSpan.FromSeconds(30);

    public bool CanConfirmRootObjects => workspaces is IProjectAuthorizationConfirmation { CanConfirmRootObjects: true };

    public Task<AuthorizationResourceInspection> InspectAuthorizationResourcesAsync(
        Guid projectId, CancellationToken cancellationToken) =>
        authorizations?.InspectResourcesAsync(projectId, _authorizationStaleAfter, cancellationToken)
        ?? Task.FromResult(AuthorizationResourceInspection.Empty);

    public Task<int> AbandonStaleAuthorizationResourcesAsync(
        Guid projectId, IReadOnlyCollection<Guid> leaseIds, CancellationToken cancellationToken) =>
        authorizations?.AbandonStaleResourcesAsync(projectId, leaseIds, _authorizationStaleAfter, cancellationToken)
        ?? throw new AuthorizationResourceChangedException();

    public Task<IReadOnlyList<WorktreeCreationIntent>> InspectWorktreeRecoveryAsync(
        Guid projectId, CancellationToken cancellationToken) =>
        worktrees is IWorktreeCreationIntentService intents
            ? intents.ReadIncompleteIntentsAsync(projectId, cancellationToken)
            : Task.FromResult<IReadOnlyList<WorktreeCreationIntent>>([]);

    public Task AcknowledgeWorktreeRecoveryAsync(
        WorktreeCreationIntent intent, CancellationToken cancellationToken) =>
        worktrees is IWorktreeCreationIntentService service
            ? service.AcknowledgeAsync(intent.TaskId, intent.TargetPath, cancellationToken)
            : throw new InvalidOperationException("Worktree recovery is unavailable.");

    public async Task<ProjectProfile> ConfirmRootObjectsAsync(ProjectProfile expected, CancellationToken cancellationToken)
    {
        if (workspaces is not IProjectAuthorizationConfirmation { CanConfirmRootObjects: true } confirmation)
            throw new ProjectRootAuthorizationException();
        var saved = await confirmation.ConfirmRootObjectsAsync(expected, cancellationToken);
        return await EnsureDrainedAsync(saved, expected.AuthorizationRevision);
    }

    public async Task<ProjectProfile> CreateAsync(
        string name,
        IReadOnlyList<string> directories,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var project = WorkspaceProjectFactory.Create(name, directories);
        return await SaveAsync(project, 0, cancellationToken);
    }

    public async Task<ProjectProfile> UpdateAsync(
        ProjectProfile existing,
        string name,
        IReadOnlyList<string> directories,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = WorkspaceProjectFactory.Create(name, directories);
        var updated = validated with { Id = existing.Id, CreatedAt = existing.CreatedAt, AuthorizationRevision = existing.AuthorizationRevision };
        return await SaveAsync(updated, existing.AuthorizationRevision, cancellationToken);
    }

    private async Task<ProjectProfile> SaveAsync(ProjectProfile project, long previousRevision, CancellationToken cancellationToken)
    {
        ProjectProfile saved;
        if (workspaces is IProjectAuthorizationWriter writer)
            saved = await writer.SaveAuthorizationAsync(project, cancellationToken);
        else
        {
            await workspaces.SaveProjectAsync(project, cancellationToken);
            saved = project;
        }
        return await EnsureDrainedAsync(saved, previousRevision);
    }

    private async Task<ProjectProfile> EnsureDrainedAsync(ProjectProfile saved, long previousRevision)
    {
        if (authorizations is null || saved.AuthorizationRevision <= previousRevision) return saved;

        var drain = await authorizations.WaitForQuiescenceAsync(
            saved.Id, previousRevision, _authorizationDrainTimeout).ConfigureAwait(false);
        if (!drain.IsDrained)
            throw new ProjectAuthorizationNotQuiescedException(saved, drain.OutstandingOperations);
        return saved;
    }

    public async Task<(string Path, string SuggestedName)?> PickAsync(string currentName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = await folderPicker.PickFolderAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path)) return null;
        var suggestedName = string.IsNullOrWhiteSpace(currentName)
            ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))
            : currentName;
        return (path, suggestedName);
    }
}
