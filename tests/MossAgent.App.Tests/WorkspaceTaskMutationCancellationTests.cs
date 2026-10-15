using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceTaskMutationCancellationTests
{
    [Fact]
    public async Task RenameDeadline_CancelsTheRepositoryAndKeepsTheDraft()
    {
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (_, token) => Task.Delay(Timeout.Infinite, token)
        };
        using var management = CreateManagement(repository, TimeSpan.FromMilliseconds(20));
        var published = false;
        management.TaskRenamed += _ => published = true;

        await management.SaveRenameCommand.ExecuteAsync(null)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(management.HasStatusError);
        Assert.Equal(SettingsText.Get("TaskMutationTimedOut"), management.Status);
        Assert.True(management.IsEditing);
        Assert.Equal("saved title", management.EditingTitle);
        Assert.False(management.IsBusy);
        Assert.False(published);
    }

    [Fact]
    public async Task ExplicitRenameCancellation_DoesNotReportStorageFailure()
    {
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (_, token) => Task.Delay(Timeout.Infinite, token)
        };
        using var management = CreateManagement(repository, TimeSpan.FromSeconds(30));
        var pending = management.SaveRenameCommand.ExecuteAsync(null);
        management.SaveRenameCommand.Cancel();
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(management.HasStatusError);
        Assert.Empty(management.Status);
        Assert.True(management.IsEditing);
        Assert.Equal("saved title", management.EditingTitle);
        Assert.False(management.IsBusy);
        Assert.True(management.SaveRenameCommand.CanExecute(null));
    }

    private static WorkspaceTaskManagementViewModel CreateManagement(TestWorkspaceRepository repository, TimeSpan timeout)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), Guid.NewGuid(), "original", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, null);
        var management = new WorkspaceTaskManagementViewModel(repository, timeout) { SelectedTask = task };
        management.BeginRenameCommand.Execute(task);
        management.EditingTitle = "saved title";
        return management;
    }
}
