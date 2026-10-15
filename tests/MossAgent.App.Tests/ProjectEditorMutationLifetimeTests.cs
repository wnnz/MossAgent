using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectEditorMutationLifetimeTests
{
    [Fact]
    public async Task PendingSave_RejectsOverlapAndForwardsCancellation()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var received = CancellationToken.None;
        var repository = new TestWorkspaceRepository
        {
            SaveProjectHandler = (_, token) => { calls++; received = token; return release.Task; }
        };
        using var editor = Create(repository);
        var saving = editor.AddCommand.ExecuteAsync(null);
        Assert.True(editor.IsBusy);
        Assert.True(editor.IsSaving);
        Assert.Equal("正在保存…", editor.SubmitLabel);
        Assert.False(editor.AddCommand.CanExecute(null));
        Assert.False(editor.AddDirectoryCommand.CanExecute(null));
        Assert.True(received.CanBeCanceled);
        await editor.AddCommand.ExecuteAsync(null);
        Assert.Equal(1, calls);
        release.SetResult();
        await saving;
        Assert.False(editor.IsBusy);
        Assert.False(editor.HasStatusError);
    }

    [Fact]
    public async Task DisposedOwner_CancelsStorageAndRejectsLatePublication()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = CancellationToken.None;
        var repository = new TestWorkspaceRepository
        {
            SaveProjectHandler = (_, token) => { received = token; return release.Task; }
        };
        using var editor = Create(repository);
        var publications = 0;
        editor.ProjectUpdated += _ => publications++;
        var saving = editor.AddCommand.ExecuteAsync(null);
        editor.Dispose();
        Assert.True(received.IsCancellationRequested);
        Assert.False(editor.AddCommand.CanExecute(null));
        release.SetResult();
        await saving;
        Assert.Equal(0, publications);
        Assert.Null(repository.SavedProject);
        Assert.Equal("Original", editor.Name);
        Assert.True(editor.IsOpen);
    }

    [Fact]
    public async Task CommandCancellation_RetainsDraftAndAllowsRetry()
    {
        var repository = new TestWorkspaceRepository
        {
            SaveProjectHandler = (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token)
        };
        using var editor = Create(repository);
        var saving = editor.AddCommand.ExecuteAsync(null);
        editor.AddCommand.Cancel();
        await saving;
        Assert.True(editor.HasStatusError);
        Assert.Equal("保存已取消。草稿已保留。", editor.Status);
        Assert.Equal("Original", editor.Name);
        Assert.Single(editor.Directories);
        repository.SaveProjectHandler = null;
        await editor.AddCommand.ExecuteAsync(null);
        Assert.False(editor.IsOpen);
        Assert.Equal("Original", repository.SavedProject?.Name);
    }

    [Fact]
    public async Task LateDirectorySelection_CannotChangeAnotherProjectDraft()
    {
        var picker = new DeferredFolderPickerService();
        using var editor = new WorkspaceProjectEditorViewModel(new WorkspaceProjectService(new TestWorkspaceRepository(), picker));
        editor.OpenCommand.Execute(null);
        var picking = editor.AddDirectoryCommand.ExecuteAsync(null);
        var other = new ProjectProfile(Guid.NewGuid(), "Other", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow);
        editor.Edit(other);
        picker.Complete(Path.Combine(Path.GetTempPath(), "stale-selection"));
        await picking;
        Assert.Equal("Other", editor.Name);
        Assert.Equal(Path.GetTempPath(), Assert.Single(editor.Directories).Path);
        Assert.True(editor.IsOpen);
        Assert.False(editor.IsBusy);
    }

    [Fact]
    public async Task DisposedPicker_CannotAddLateDirectoryOrReopenEditor()
    {
        var picker = new DeferredFolderPickerService();
        using var editor = new WorkspaceProjectEditorViewModel(new WorkspaceProjectService(new TestWorkspaceRepository(), picker));
        editor.OpenCommand.Execute(null);
        var picking = editor.AddDirectoryCommand.ExecuteAsync(null);
        editor.IsOpen = false;
        editor.Dispose();
        await picking;
        picker.Complete(Path.GetTempPath());
        Assert.Empty(editor.Directories);
        Assert.False(editor.IsOpen);
        Assert.False(editor.AddDirectoryCommand.CanExecute(null));
    }

    [Fact]
    public async Task DirectorySuggestion_CannotOverwriteAnEditedAndClearedName()
    {
        var picker = new DeferredFolderPickerService();
        using var editor = new WorkspaceProjectEditorViewModel(new WorkspaceProjectService(new TestWorkspaceRepository(), picker));
        editor.OpenCommand.Execute(null);
        var picking = editor.AddDirectoryCommand.ExecuteAsync(null);
        editor.Name = "User revision";
        editor.Name = string.Empty;
        picker.Complete(Path.GetTempPath());
        await picking;
        Assert.Empty(editor.Name);
        Assert.Single(editor.Directories);
    }

    [Fact]
    public async Task InvalidDirectory_DoesNotLeakTheRejectedPath()
    {
        using var editor = Create(new TestWorkspaceRepository());
        editor.Edit(new ProjectProfile(Guid.NewGuid(), "Invalid", "secret-path\0", ["secret-path\0"], DateTimeOffset.UtcNow));
        await editor.AddCommand.ExecuteAsync(null);
        Assert.True(editor.IsOpen);
        Assert.True(editor.HasStatusError);
        Assert.DoesNotContain("secret-path", editor.Status);
        Assert.Equal("一个或多个授权目录不存在或路径无效，请检查后重试。", editor.Status);
    }

    private static WorkspaceProjectEditorViewModel Create(TestWorkspaceRepository repository)
    {
        var editor = new WorkspaceProjectEditorViewModel(new WorkspaceProjectService(repository,
            new TestFolderPickerService(Path.GetTempPath())));
        editor.Edit(new ProjectProfile(Guid.NewGuid(), "Original", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow));
        return editor;
    }
}
