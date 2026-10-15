using MossAgent.Application.Persistence;
using MossAgent.App.Services;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceProjectService(
    IWorkspaceRepository workspaces,
    IFolderPickerService folderPicker)
{
    public async Task<ProjectProfile> CreateAsync(
        string name,
        IReadOnlyList<string> directories,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var project = WorkspaceProjectFactory.Create(name, directories);
        await workspaces.SaveProjectAsync(project, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return project;
    }

    public async Task<ProjectProfile> UpdateAsync(
        ProjectProfile existing,
        string name,
        IReadOnlyList<string> directories,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = WorkspaceProjectFactory.Create(name, directories);
        var updated = validated with { Id = existing.Id, CreatedAt = existing.CreatedAt };
        await workspaces.SaveProjectAsync(updated, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return updated;
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
