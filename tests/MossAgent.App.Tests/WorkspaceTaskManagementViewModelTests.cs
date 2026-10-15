using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceTaskManagementViewModelTests
{
    [Fact]
    public async Task RenameArchiveAndRestore_RaiseExpectedChanges()
    {
        var repository = new TestWorkspaceRepository();
        var task = CreateTask("original");
        var management = new WorkspaceTaskManagementViewModel(repository)
        {
            SelectedTask = task
        };
        AgentTask? renamed = null;
        AgentTask? archived = null;
        AgentTask? restored = null;
        management.TaskRenamed += value => renamed = value;
        management.TaskArchived += value => archived = value;
        management.TaskRestored += value => restored = value;

        management.BeginRenameCommand.Execute(null);
        management.EditingTitle = "renamed";
        await management.SaveRenameCommand.ExecuteAsync(null);
        await management.ArchiveCommand.ExecuteAsync(null);
        management.SelectedArchivedTask = archived;
        await management.RestoreCommand.ExecuteAsync(null);

        Assert.Equal("renamed", repository.SavedTask?.Title);
        Assert.Equal("renamed", renamed?.Title);
        Assert.Equal("renamed", archived?.Title);
        Assert.Same(archived, restored);
        Assert.Equal([(task.Id, true), (task.Id, false)], repository.ArchiveOperations);
    }

    [Fact]
    public void RunningTask_CannotBeRenamedOrArchived()
    {
        var task = CreateTask("running");
        var management = new WorkspaceTaskManagementViewModel(new TestWorkspaceRepository())
        {
            IsTaskRunning = id => id == task.Id,
            SelectedTask = task
        };

        Assert.False(management.BeginRenameCommand.CanExecute(null));
        Assert.False(management.ArchiveCommand.CanExecute(null));
        Assert.True(management.TogglePinCommand.CanExecute(null));
    }

    [Fact]
    public async Task TogglePin_UpdatesNavigationAndCanBeUndone()
    {
        var repository = new TestWorkspaceRepository();
        var task = CreateTask("review");
        var management = new WorkspaceTaskManagementViewModel(repository) { SelectedTask = task };
        var changes = new List<AgentTask>();
        management.TaskPinChanged += changes.Add;

        await management.TogglePinCommand.ExecuteAsync(null);
        Assert.True(management.SelectedTask?.IsPinned);
        Assert.Equal("取消置顶", management.PinToggleLabel);
        Assert.Equal(task.Id, Assert.Single(management.PinnedTasks.Items).Task.Id);

        await management.TogglePinCommand.ExecuteAsync(null);
        Assert.False(management.SelectedTask?.IsPinned);
        Assert.Equal("置顶会话", management.PinToggleLabel);
        Assert.Empty(management.PinnedTasks.Items);
        Assert.Equal([(task.Id, true), (task.Id, false)], repository.PinOperations);
        Assert.Equal([true, false], changes.Select(change => change.IsPinned));
    }

    [Fact]
    public async Task TogglePin_RejectedPersistenceLeavesSelectionUnchanged()
    {
        var repository = new TestWorkspaceRepository { RejectPinChange = true };
        var task = CreateTask("review");
        var management = new WorkspaceTaskManagementViewModel(repository) { SelectedTask = task };
        var changes = 0;
        management.TaskPinChanged += _ => changes++;

        await management.TogglePinCommand.ExecuteAsync(null);

        Assert.Same(task, management.SelectedTask);
        Assert.Empty(management.PinnedTasks.Items);
        Assert.Equal(0, changes);
        Assert.Contains("已归档", management.Status);
    }

    [Fact]
    public async Task TogglePin_DoesNotReselectTaskAfterNavigationChanges()
    {
        var completion = new TaskCompletionSource<bool>();
        var repository = new TestWorkspaceRepository
        {
            PinHandler = (_, _, _) => completion.Task
        };
        var first = CreateTask("first");
        var second = CreateTask("second");
        var management = new WorkspaceTaskManagementViewModel(repository) { SelectedTask = first };

        var pending = management.TogglePinCommand.ExecuteAsync(null);
        management.SelectedTask = second;
        completion.SetResult(true);
        await pending;

        Assert.Same(second, management.SelectedTask);
        Assert.Equal(first.Id, Assert.Single(management.PinnedTasks.Items).Task.Id);
    }

    private static AgentTask CreateTask(string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentTask(
            Guid.NewGuid(), Guid.NewGuid(), title, ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Completed, now, now, null);
    }
}
