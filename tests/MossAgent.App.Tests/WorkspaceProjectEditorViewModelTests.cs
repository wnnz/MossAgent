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
            repository, new TestFolderPickerService("D:\\Dev\\MossAgent"));
        var editor = new WorkspaceProjectEditorViewModel(service)
        {
            IsOpen = true,
            Name = "MossAgent"
        };
        ProjectProfile? created = null;
        editor.ProjectCreated += project => created = project;

        await editor.AddDirectoryCommand.ExecuteAsync(null);
        await editor.AddCommand.ExecuteAsync(null);

        Assert.False(editor.IsOpen);
        Assert.Equal("项目已添加。", editor.Status);
        Assert.Same(repository.SavedProject, created);
    }

    [Fact]
    public async Task AddDirectoryCommand_AddsPathAndSuggestedName()
    {
        var repository = new TestWorkspaceRepository();
        var service = new WorkspaceProjectService(
            repository, new TestFolderPickerService("D:\\Dev\\MossAgent"));
        var editor = new WorkspaceProjectEditorViewModel(service);

        await editor.AddDirectoryCommand.ExecuteAsync(null);

        Assert.Equal("D:\\Dev\\MossAgent", Assert.Single(editor.Directories).Path);
        Assert.Equal("MossAgent", editor.Name);
    }

    [Fact]
    public async Task SetPrimaryCommand_MovesDirectoryToFirstAndPersistsItAsPrimary()
    {
        var repository = new TestWorkspaceRepository();
        var service = new WorkspaceProjectService(
            repository, new TestFolderPickerService("D:\\Dev\\MossAgent", "D:\\Dev"));
        var editor = new WorkspaceProjectEditorViewModel(service) { Name = "多目录项目" };

        await editor.AddDirectoryCommand.ExecuteAsync(null);
        await editor.AddDirectoryCommand.ExecuteAsync(null);
        editor.Directories[1].SetPrimaryCommand.Execute(null);
        await editor.AddCommand.ExecuteAsync(null);

        Assert.Equal("D:\\Dev", repository.SavedProject?.PrimaryDirectory);
        Assert.Equal("D:\\Dev", repository.SavedProject?.AuthorizedDirectories[0]);
    }
}
