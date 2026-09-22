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
        IReadOnlyList<string> directories)
    {
        var project = WorkspaceProjectFactory.Create(name, directories);
        await workspaces.SaveProjectAsync(project, CancellationToken.None);
        return project;
    }

    public async Task<(string Path, string SuggestedName)?> PickAsync(string currentName)
    {
        var path = await folderPicker.PickFolderAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path)) return null;
        var suggestedName = string.IsNullOrWhiteSpace(currentName)
            ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))
            : currentName;
        return (path, suggestedName);
    }
}
