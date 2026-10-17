using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectEditorCommandStateTests
{
    [Fact]
    public void Close_PreservesDraftAndCannotRunAfterDisposal()
    {
        using var editor = Create(new TestWorkspaceRepository(), new TestFolderPickerService());
        editor.OpenCommand.Execute(null);
        editor.Name = "尚未保存的草稿";
        Assert.True(editor.CloseCommand.CanExecute(null));
        editor.CloseCommand.Execute(null);
        Assert.False(editor.IsOpen);
        Assert.Equal("尚未保存的草稿", editor.Name);
        editor.OpenCommand.Execute(null);
        Assert.Equal("尚未保存的草稿", editor.Name);
        editor.Dispose();
        Assert.False(editor.CloseCommand.CanExecute(null));
    }

    [Fact]
    public async Task PendingSave_DisablesCloseUntilStorageReturns()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestWorkspaceRepository { SaveProjectHandler = (_, _) => release.Task };
        using var editor = Create(repository, new TestFolderPickerService());
        editor.Edit(new ProjectProfile(Guid.NewGuid(), "保存中的项目", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow));
        var notifications = 0;
        editor.CloseCommand.CanExecuteChanged += (_, _) => notifications++;
        var saving = editor.AddCommand.ExecuteAsync(null);
        Assert.True(editor.IsSaving);
        Assert.False(editor.CloseCommand.CanExecute(null));
        Assert.False(editor.OpenCommand.CanExecute(null));
        editor.CloseCommand.Execute(null);
        editor.OpenCommand.Execute(null);
        Assert.True(editor.IsOpen);
        Assert.Equal("保存中的项目", editor.Name);
        release.SetResult();
        await saving;
        Assert.True(editor.CloseCommand.CanExecute(null));
        Assert.False(editor.IsOpen);
        Assert.True(notifications >= 2);
    }

    [Fact]
    public async Task PendingPicker_DisablesCloseAndRestoresItAfterCancellation()
    {
        var picker = new DeferredFolderPickerService();
        using var editor = Create(new TestWorkspaceRepository(), picker);
        editor.OpenCommand.Execute(null);
        var picking = editor.AddDirectoryCommand.ExecuteAsync(null);
        Assert.True(editor.IsPickingDirectory);
        Assert.False(editor.CloseCommand.CanExecute(null));
        picker.Complete(null);
        await picking;
        Assert.True(editor.IsOpen);
        Assert.True(editor.CloseCommand.CanExecute(null));
        Assert.False(editor.HasDirectories);
    }

    [Fact]
    public async Task LockedProject_StillAllowsDismissWithoutSaving()
    {
        var repository = new TestWorkspaceRepository();
        using var editor = Create(repository, new TestFolderPickerService());
        editor.CanSave = () => false;
        editor.OpenCommand.Execute(null);
        editor.NotifyAvailabilityChanged();
        Assert.True(editor.HasStatus);
        Assert.True(editor.HasStatusError);
        Assert.False(editor.AddCommand.CanExecute(null));
        Assert.True(editor.CloseCommand.CanExecute(null));
        editor.CloseCommand.Execute(null);
        await editor.AddCommand.ExecuteAsync(null);
        Assert.Null(repository.SavedProject);
        Assert.False(editor.IsOpen);
    }

    private static WorkspaceProjectEditorViewModel Create(TestWorkspaceRepository repository,
        MossAgent.App.Services.IFolderPickerService picker) => new(new WorkspaceProjectService(repository, picker));
}
