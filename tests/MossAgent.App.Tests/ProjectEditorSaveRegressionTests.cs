using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectEditorSaveRegressionTests
{
    [Fact]
    public async Task StorageFailure_DoesNotExposeDetailsAndRetainsDraft()
    {
        var repository = new TestWorkspaceRepository
        {
            SaveProjectHandler = (_, _) => throw new IOException("api-key=synthetic-secret private-storage-detail")
        };
        var editor = await CreateAsync(repository);
        await editor.AddCommand.ExecuteAsync(null);
        Assert.DoesNotContain("synthetic-secret", editor.Status);
        Assert.DoesNotContain("private-storage-detail", editor.Status);
        Assert.Equal("Original", editor.Name);
        Assert.Single(editor.Directories);
        Assert.True(editor.IsOpen);
        Assert.Null(repository.SavedProject);
    }

    [Fact]
    public async Task LateSave_CannotClearANewerDraft()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestWorkspaceRepository { SaveProjectHandler = (_, _) => release.Task };
        var editor = await CreateAsync(repository);
        ProjectProfile? published = null;
        editor.ProjectCreated += project => published = project;
        var saving = editor.AddCommand.ExecuteAsync(null);
        editor.Name = "Newer draft";
        release.SetResult();
        await saving;
        Assert.Equal("Original", repository.SavedProject?.Name);
        Assert.Same(repository.SavedProject, published);
        Assert.Equal("Newer draft", editor.Name);
        Assert.Single(editor.Directories);
        Assert.True(editor.IsOpen);
    }

    [Fact]
    public async Task LateSave_CannotClearAnotherProjectEditor()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestWorkspaceRepository { SaveProjectHandler = (_, _) => release.Task };
        var editor = await CreateAsync(repository);
        var saving = editor.AddCommand.ExecuteAsync(null);
        var other = new ProjectProfile(Guid.NewGuid(), "Other", Path.GetTempPath(),
            [Path.GetTempPath()], DateTimeOffset.UtcNow);
        editor.Edit(other);
        release.SetResult();
        await saving;
        Assert.Equal("Other", editor.Name);
        Assert.Equal("编辑项目", editor.EditorTitle);
        Assert.True(editor.IsOpen);
        Assert.Single(editor.Directories);
        Assert.NotEqual(other.Id, repository.SavedProject?.Id);
    }

    private static async Task<WorkspaceProjectEditorViewModel> CreateAsync(TestWorkspaceRepository repository)
    {
        var editor = new WorkspaceProjectEditorViewModel(new WorkspaceProjectService(repository,
            new TestFolderPickerService(Path.GetTempPath())))
        { Name = "Original", IsOpen = true };
        await editor.AddDirectoryCommand.ExecuteAsync(null);
        return editor;
    }
}
