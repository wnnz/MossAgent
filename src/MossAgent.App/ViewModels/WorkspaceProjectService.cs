using MossAgent.Application.Persistence;
using MossAgent.App.Services;
using MossAgent.Domain;
using MossAgent.Application.Authorization;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceProjectService(
    IWorkspaceRepository workspaces,
    IFolderPickerService folderPicker)
{
    public bool CanConfirmRootObjects => workspaces is IProjectAuthorizationConfirmation { CanConfirmRootObjects: true };

    public Task<ProjectProfile> ConfirmRootObjectsAsync(ProjectProfile expected, CancellationToken cancellationToken) =>
        workspaces is IProjectAuthorizationConfirmation { CanConfirmRootObjects: true } confirmation
            ? confirmation.ConfirmRootObjectsAsync(expected, cancellationToken)
            : throw new ProjectRootAuthorizationException();

    public async Task<ProjectProfile> CreateAsync(
        string name,
        IReadOnlyList<string> directories,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var project = WorkspaceProjectFactory.Create(name, directories);
        return await SaveAsync(project, cancellationToken);
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
        return await SaveAsync(updated, cancellationToken);
    }

    private async Task<ProjectProfile> SaveAsync(ProjectProfile project, CancellationToken cancellationToken)
    {
        if (workspaces is IProjectAuthorizationWriter writer)
            return await writer.SaveAuthorizationAsync(project, cancellationToken);
        await workspaces.SaveProjectAsync(project, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return project;
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
