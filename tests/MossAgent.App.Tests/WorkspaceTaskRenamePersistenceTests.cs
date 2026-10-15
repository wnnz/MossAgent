using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceTaskRenamePersistenceTests
{
    [Theory]
    [InlineData(AgentTaskStatus.Failed)]
    [InlineData(AgentTaskStatus.Cancelled)]
    [InlineData(AgentTaskStatus.Interrupted)]
    public async Task Rename_PreservesNewerMetadataAndPublishesTheStoredSnapshot(AgentTaskStatus status)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var original = fixture.Task with { Status = AgentTaskStatus.Completed };
        await fixture.Repository.SaveTaskAsync(original, fixture.Token);
        using var management = BeginRename(fixture, original);
        var latest = original with
        {
            Status = status,
            ApprovalPolicy = ApprovalPolicy.FullAccess,
            WorktreePath = Path.Combine(fixture.Project.PrimaryDirectory, "new-worktree"),
            UpdatedAt = DateTimeOffset.UtcNow,
            FailureReason = status == AgentTaskStatus.Failed ? TaskFailureReason.ProviderTimeout : null
        };
        await fixture.Repository.SaveTaskAsync(latest, fixture.Token);
        await fixture.Repository.SetTaskPinnedAsync(original.Id, true, fixture.Token);

        await management.SaveRenameCommand.ExecuteAsync(null);

        var saved = Assert.Single(await fixture.Repository.GetTasksAsync(original.ProjectId, fixture.Token));
        Assert.Equal("new title", saved.Title);
        Assert.Equal(latest.Status, saved.Status);
        Assert.Equal(latest.ApprovalPolicy, saved.ApprovalPolicy);
        Assert.Equal(latest.WorktreePath, saved.WorktreePath);
        Assert.Equal(latest.FailureReason, saved.FailureReason);
        Assert.Equal(latest.CreatedAt, saved.CreatedAt);
        Assert.True(saved.IsPinned);
        Assert.Equal(saved, management.SelectedTask);
        Assert.Equal(saved, Assert.Single(management.PinnedTasks.Items).Task);
        Assert.Single(await fixture.Repository.GetMessagesAsync(original.Id, fixture.Token));
        Assert.False(management.IsEditing);
    }

    [Theory]
    [InlineData(AgentTaskStatus.Running)]
    [InlineData(AgentTaskStatus.WaitingForApproval)]
    public async Task TaskStartedAfterOpeningEditor_RejectsRenameWithoutRewindingStatus(AgentTaskStatus status)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var original = fixture.Task with { Status = AgentTaskStatus.Completed };
        await fixture.Repository.SaveTaskAsync(original, fixture.Token);
        using var management = BeginRename(fixture, original);
        var latest = original with { Status = status, ApprovalPolicy = ApprovalPolicy.AskEveryTime };
        await fixture.Repository.SaveTaskAsync(latest, fixture.Token);

        await management.SaveRenameCommand.ExecuteAsync(null);

        Assert.Equal(latest, Assert.Single(await fixture.Repository.GetTasksAsync(original.ProjectId, fixture.Token)));
        Assert.True(management.HasStatusError);
        Assert.True(management.IsEditing);
        Assert.Equal("new title", management.EditingTitle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleTitleOrArchivedTarget_RejectsRenameAndKeepsTheDraft(bool archived)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var original = fixture.Task with { Status = AgentTaskStatus.Completed };
        await fixture.Repository.SaveTaskAsync(original, fixture.Token);
        using var management = BeginRename(fixture, original);
        var latest = archived ? original : original with { Title = "newer external title" };
        if (archived) await fixture.Repository.SetTaskArchivedAsync(original.Id, true, fixture.Token);
        else await fixture.Repository.SaveTaskAsync(latest, fixture.Token);
        var published = false;
        management.TaskRenamed += _ => published = true;

        await management.SaveRenameCommand.ExecuteAsync(null);

        var tasks = archived
            ? await fixture.Repository.GetArchivedTasksAsync(original.ProjectId, fixture.Token)
            : await fixture.Repository.GetTasksAsync(original.ProjectId, fixture.Token);
        Assert.Equal(latest, Assert.Single(tasks));
        Assert.True(management.HasStatusError);
        Assert.Contains("草稿", management.Status);
        Assert.Equal("new title", management.EditingTitle);
        Assert.False(published);
    }

    [Fact]
    public async Task MissingTarget_IsNotRecreatedByRename()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var missing = fixture.Task with { Id = Guid.NewGuid(), Status = AgentTaskStatus.Completed };
        using var management = BeginRename(fixture, missing);

        await management.SaveRenameCommand.ExecuteAsync(null);

        Assert.Equal(fixture.Task.Id, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).Id);
        Assert.True(management.IsEditing);
        Assert.True(management.HasStatusError);
    }

    private static WorkspaceTaskManagementViewModel BeginRename(WorkspacePersistenceFixture fixture, AgentTask task)
    {
        var management = new WorkspaceTaskManagementViewModel(fixture.Repository) { SelectedTask = task };
        management.BeginRenameCommand.Execute(task);
        management.EditingTitle = "new title";
        return management;
    }
}
