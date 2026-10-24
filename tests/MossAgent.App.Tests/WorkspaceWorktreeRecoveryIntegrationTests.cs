using MossAgent.App.ViewModels;
using MossAgent.Application.Git;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceWorktreeRecoveryIntegrationTests
{
    [Fact]
    public async Task AcknowledgeClosesOrphanRecordWithoutTouchingTasksOrRequests()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var project = workspace.Catalog.SelectedProject!;
        var intents = Assert.IsAssignableFrom<IWorktreeCreationIntents>(fixture.Worktrees);
        var taskId = await WorkspaceWorktreeRecoverySeeding.SeedMissingOrphanAsync(fixture, project);

        await workspace.WorktreeRecovery.RefreshAsync();

        Assert.True(workspace.WorktreeRecovery.IsVisible);
        var row = Assert.Single(workspace.WorktreeRecovery.Items);
        Assert.Equal(taskId, row.TaskId);
        Assert.Contains("目标目录不存在", row.Summary);
        var draft = workspace.ComposerText;

        await row.AcknowledgeCommand.ExecuteAsync(null);

        Assert.Empty(workspace.WorktreeRecovery.Items);
        Assert.Empty(await intents.ReadIncompleteAsync(project, fixture.Token));
        Assert.Equal(draft, workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(project.Id, fixture.Token));
    }

    [Fact]
    public async Task CleanupOrphanListsAndAcknowledgeClosesRecordWithoutTouchingTasks()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var project = workspace.Catalog.SelectedProject!;
        var cleanupIntents = Assert.IsAssignableFrom<IWorktreeCleanupIntents>(fixture.Worktrees);
        var taskId = await WorkspaceWorktreeRecoverySeeding.SeedRemovedCleanupOrphanAsync(fixture, project);

        await workspace.WorktreeRecovery.RefreshAsync();

        var row = Assert.Single(workspace.WorktreeRecovery.Items);
        Assert.Equal(taskId, row.TaskId);
        Assert.True(row.IsCleanupItem);
        Assert.Contains("清理中断", row.Summary);

        await row.AcknowledgeCommand.ExecuteAsync(null);

        Assert.Empty(workspace.WorktreeRecovery.Items);
        Assert.Empty(await cleanupIntents.ReadIncompleteAsync(project, fixture.Token));
        Assert.Empty(await fixture.Tasks.GetTasksAsync(project.Id, fixture.Token));
    }
}
