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
    }

    private static AgentTask CreateTask(string title)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentTask(
            Guid.NewGuid(), Guid.NewGuid(), title, ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Completed, now, now, null);
    }
}
