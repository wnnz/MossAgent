using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceProjectEditorViewModelTests
{
    [Fact]
    public void OpenCommand_OpensEditor()
    {
        var service = new WorkspaceProjectService(
            new TestWorkspaceRepository(), new TestFolderPickerService("D:\\Dev\\MossAgent"));
        var editor = new WorkspaceProjectEditorViewModel(service);

        editor.OpenCommand.Execute(null);

        Assert.True(editor.IsOpen);
    }

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

    [Theory]
    [InlineData("D:\\Dev\\MossAgent", 1)]
    [InlineData(null, 0)]
    public async Task AddDirectoryCommand_RestoresEditorAfterNativePickerCloses(
        string? selectedPath, int expectedDirectoryCount)
    {
        var picker = new DeferredFolderPickerService();
        var editor = new WorkspaceProjectEditorViewModel(
            new WorkspaceProjectService(new TestWorkspaceRepository(), picker))
        {
            IsOpen = true
        };

        var adding = editor.AddDirectoryCommand.ExecuteAsync(null);
        editor.IsOpen = false;
        picker.Complete(selectedPath);
        await adding;

        Assert.True(editor.IsOpen);
        Assert.Equal(expectedDirectoryCount, editor.Directories.Count);
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

    [Fact]
    public async Task EditProject_UpdatesCatalogAndPreservesIdentity()
    {
        var repository = new TestWorkspaceRepository();
        var catalog = new WorkspaceCatalogViewModel(repository,
            new WorkspaceWorktreeCleanupService(repository, null!),
            new WorkspaceProjectService(repository, new TestFolderPickerService("D:\\Dev\\MossAgent")));
        catalog.ProjectEditor.Name = "Original";
        await catalog.ProjectEditor.AddDirectoryCommand.ExecuteAsync(null);
        await catalog.ProjectEditor.AddCommand.ExecuteAsync(null);
        var original = Assert.Single(catalog.Projects);

        catalog.EditProjectCommand.Execute(Assert.Single(catalog.ProjectNavigation));
        Assert.True(catalog.ProjectEditor.IsOpen);
        Assert.Equal("编辑项目", catalog.ProjectEditor.EditorTitle);
        Assert.Equal("保存项目", catalog.ProjectEditor.SubmitLabel);
        catalog.ProjectEditor.Name = "Renamed";
        await catalog.ProjectEditor.AddCommand.ExecuteAsync(null);

        var updated = Assert.Single(catalog.Projects);
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.Equal("Renamed", updated.Name);
        Assert.Equal("Renamed", Assert.Single(catalog.ProjectNavigation).Project.Name);
        Assert.Equal("Renamed", catalog.SelectedProject?.Name);
        Assert.Same(updated, repository.SavedProject);
        Assert.Equal("项目已保存。", catalog.ProjectEditor.Status);
    }

    [Fact]
    public async Task EditProject_RejectsSavingWhileTaskIsRunning()
    {
        var repository = new TestWorkspaceRepository();
        var catalog = new WorkspaceCatalogViewModel(repository,
            new WorkspaceWorktreeCleanupService(repository, null!),
            new WorkspaceProjectService(repository, new TestFolderPickerService("D:\\Dev\\MossAgent")));
        catalog.ProjectEditor.Name = "Original";
        await catalog.ProjectEditor.AddDirectoryCommand.ExecuteAsync(null);
        await catalog.ProjectEditor.AddCommand.ExecuteAsync(null);
        var original = Assert.Single(catalog.Projects);

        var group = Assert.Single(catalog.ProjectNavigation);
        group.IsSummaryOpen = true;
        catalog.EditProjectCommand.Execute(group);
        Assert.False(group.IsSummaryOpen);
        catalog.ProjectEditor.Name = "Blocked";
        catalog.IsLocked = true;
        Assert.False(catalog.EditProjectCommand.CanExecute(group));
        await catalog.ProjectEditor.AddCommand.ExecuteAsync(null);

        Assert.Equal("任务运行时不能修改项目。", catalog.ProjectEditor.Status);
        Assert.Same(original, repository.SavedProject);
        Assert.Equal("Original", Assert.Single(catalog.Projects).Name);
    }

    [Fact]
    public async Task EditProject_RejectsRemovingEveryAuthorizedDirectory()
    {
        var repository = new TestWorkspaceRepository();
        var service = new WorkspaceProjectService(
            repository, new TestFolderPickerService("D:\\Dev\\MossAgent"));
        var editor = new WorkspaceProjectEditorViewModel(service) { Name = "Original" };
        await editor.AddDirectoryCommand.ExecuteAsync(null);
        await editor.AddCommand.ExecuteAsync(null);
        var original = Assert.IsType<ProjectProfile>(repository.SavedProject);

        editor.Edit(original);
        editor.Directories.Clear();
        await editor.AddCommand.ExecuteAsync(null);

        Assert.True(editor.IsOpen);
        Assert.Equal("请填写项目名称并至少添加一个目录。", editor.Status);
        Assert.Same(original, repository.SavedProject);
    }
}
