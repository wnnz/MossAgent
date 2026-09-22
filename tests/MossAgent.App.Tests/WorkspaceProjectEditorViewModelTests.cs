using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceProjectEditorViewModelTests
{
    [Fact]
    public async Task AddCommand_SavesProjectAndClosesEditor()
    {
        var repository = new TestWorkspaceRepository();
        var service = new WorkspaceProjectService(
            repository, new TestFolderPickerService(null));
        var editor = new WorkspaceProjectEditorViewModel(service)
        {
            IsOpen = true,
            Name = "MossAgent",
            Directory = "D:\\Dev\\MossAgent"
        };
        ProjectProfile? created = null;
        editor.ProjectCreated += project => created = project;

        await editor.AddCommand.ExecuteAsync(null);

        Assert.False(editor.IsOpen);
        Assert.Equal("项目已添加。", editor.Status);
        Assert.Same(repository.SavedProject, created);
    }

    [Fact]
    public async Task BrowseDirectoryCommand_FillsPathAndSuggestedName()
    {
        var repository = new TestWorkspaceRepository();
        var service = new WorkspaceProjectService(
            repository, new TestFolderPickerService("D:\\Dev\\MossAgent"));
        var editor = new WorkspaceProjectEditorViewModel(service);

        await editor.BrowseDirectoryCommand.ExecuteAsync(null);

        Assert.Equal("D:\\Dev\\MossAgent", editor.Directory);
        Assert.Equal("MossAgent", editor.Name);
    }
}
