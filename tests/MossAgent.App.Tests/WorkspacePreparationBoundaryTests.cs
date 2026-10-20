using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspacePreparationBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterRealCommit_PreservesTheNewDraftAndRestoresCancelledHistory(bool useWorktree)
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        await fixture.Workspace.Drafts.StopAsync();
        var repository = new PausingWorkspaceRepository(fixture.Tasks) { PauseAfterCommit = true, IgnoreCancellationWhilePaused = true };
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.ReadOnly);
        workspace.ComposerText = "这条消息已提交，但不能请求模型。";
        if (useWorktree) workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        AgentTask saved;
        try
        {
            workspace.PrimaryActionCommand.Execute(null);
            await repository.Entered.Task.WaitAsync(fixture.Token);
            saved = Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
            Assert.Equal(AgentTaskStatus.Running, saved.Status);
            workspace.ComposerText = "取消时新输入的草稿也必须保留。";
            workspace.PrimaryActionCommand.Execute(null);
            Assert.True(workspace.IsRunning);
            Assert.False(workspace.HasActiveRun);
            Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
            Assert.Equal("正在取消发送准备", workspace.PrimaryActionLabel);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { repository.Release.TrySetResult(); }
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        var cancelled = Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
        Assert.Equal(saved.Id, cancelled.Id);
        Assert.Equal(AgentTaskStatus.Cancelled, cancelled.Status);
        Assert.Equal(ApprovalPolicy.ReadOnly, cancelled.ApprovalPolicy);
        Assert.Equal(useWorktree, cancelled.WorktreePath is not null);
        if (useWorktree) Assert.True(Directory.Exists(cancelled.WorktreePath));
        Assert.Single(await fixture.Tasks.GetMessagesAsync(saved.Id, fixture.Token));
        Assert.Equal("取消时新输入的草稿也必须保留。", workspace.ComposerText);
        Assert.Null(workspace.Catalog.SelectedTask);
        Assert.Equal(cancelled, workspace.Catalog.FindTask(cancelled.ProjectId, cancelled.Id));
        Assert.False(workspace.HasSendPreparationError);
        Assert.Empty(fixture.Server.Requests);
        await workspace.Drafts.StopAsync();
        await AssertRestoredAsync(fixture, cancelled);
    }

    [Fact]
    public async Task CancellationStatusFailure_PreservesTheOwnedWorktreeAndDisplaysOnlySafeRecoveryInstructions()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var repository = new PausingWorkspaceRepository(fixture.Tasks) { PauseAfterCommit = true, IgnoreCancellationWhilePaused = true };
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "状态保存失败也不能发送。";
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        AgentTask saved;
        try
        {
            workspace.PrimaryActionCommand.Execute(null);
            await repository.Entered.Task.WaitAsync(fixture.Token);
            saved = Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
            await RejectCancellationAsync(fixture);
            workspace.PrimaryActionCommand.Execute(null);
        }
        finally { repository.Release.TrySetResult(); }
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.True(workspace.HasSendPreparationError);
        Assert.True(workspace.Catalog.Environment.HasPreparationNotice);
        Assert.Contains(saved.Id.ToString(), workspace.SendPreparationError);
        Assert.DoesNotContain("synthetic-secret", workspace.SendPreparationError);
        Assert.Equal("状态保存失败也不能发送。", workspace.ComposerText);
        Assert.Equal(AgentTaskStatus.Running, Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token)).Status);
        Assert.True(Directory.Exists(saved.WorktreePath));
        Assert.NotNull(workspace.Catalog.FindTask(saved.ProjectId, saved.Id));
        Assert.Empty(fixture.Server.Requests);
    }

    [Fact]
    public async Task ShutdownDoesNotAbandonAnUncooperativeSaveOrStartAProviderAfterItEventuallyReturns()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var repository = new PausingWorkspaceRepository(fixture.Tasks) { IgnoreCancellationWhilePaused = true };
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "必须等保存操作结束。";
        Task stopping;
        try
        {
            workspace.PrimaryActionCommand.Execute(null);
            await repository.Entered.Task.WaitAsync(fixture.Token);
            stopping = workspace.ShutdownAsync();
            Assert.False(stopping.IsCompleted);
            Assert.True(workspace.IsRunning);
            Assert.True(workspace.Catalog.IsLocked);
            Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
            Assert.Empty(fixture.Server.Requests);
        }
        finally { repository.Release.TrySetResult(); }
        await stopping.WaitAsync(fixture.Token);
        Assert.False(workspace.IsRunning);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(workspace.Catalog.SelectedProject!.Id, fixture.Token));
        Assert.Empty(fixture.Server.Requests);
    }

    private static async Task RejectCancellationAsync(WorkspaceModelSelectionFixture fixture)
    {
        await using var connection = new SqliteConnectionFactory(new AppDataPaths(fixture.DataRoot)).Create();
        await connection.OpenAsync(fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TRIGGER reject_preparation_cancel BEFORE UPDATE OF status ON agent_tasks
            WHEN NEW.status={(int)AgentTaskStatus.Cancelled}
            BEGIN SELECT RAISE(ABORT, 'synthetic-secret-state-refusal'); END;
            """;
        await command.ExecuteNonQueryAsync(fixture.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellingFollowUp_PreservesExistingHistoryAndNeverReplaysThePreviousTurn(bool afterCommit)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !fixture.Workspace.IsRunning);
        var original = Assert.IsType<AgentTask>(fixture.Workspace.Catalog.SelectedTask);
        await fixture.Workspace.Drafts.StopAsync();
        var repository = new PausingWorkspaceRepository(fixture.Tasks)
            { PauseAfterCommit = afterCommit, IgnoreCancellationWhilePaused = afterCommit };
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        Assert.Equal(original.Id, workspace.Catalog.SelectedTask?.Id);
        workspace.ComposerText = "这条后续消息需要保留。";
        try
        {
            workspace.PrimaryActionCommand.Execute(null);
            await repository.Entered.Task.WaitAsync(fixture.Token);
            workspace.PrimaryActionCommand.Execute(null);
        }
        finally { repository.Release.TrySetResult(); }
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        var stored = Assert.Single(await fixture.Tasks.GetTasksAsync(original.ProjectId, fixture.Token));
        Assert.Equal(original.Id, stored.Id);
        Assert.Equal(afterCommit ? AgentTaskStatus.Cancelled : AgentTaskStatus.Completed, stored.Status);
        Assert.Equal(stored, workspace.Catalog.SelectedTask);
        Assert.Equal("这条后续消息需要保留。", workspace.ComposerText);
        Assert.False(workspace.HasSendPreparationError);
        Assert.Equal(afterCommit ? 3 : 2, (await fixture.Tasks.GetMessagesAsync(original.Id, fixture.Token)).Count);
        await workspace.RefreshCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => workspace.Messages.Count == (afterCommit ? 3 : 2));
        Assert.Equal("这条后续消息需要保留。", workspace.ComposerText);
        Assert.Single(fixture.Server.Requests);
    }

    private static async Task AssertRestoredAsync(WorkspaceModelSelectionFixture fixture, AgentTask cancelled)
    {
        await using var reopened = AppComposition.CreateServices(fixture.DataRoot);
        var restored = reopened.GetRequiredService<MainWindowViewModel>().Workspace;
        await restored.LoadAsync();
        var stored = Assert.Single(await reopened.GetRequiredService<MossAgent.Application.Persistence.IWorkspaceRepository>()
            .GetTasksAsync(cancelled.ProjectId, fixture.Token));
        Assert.Equal(cancelled, stored);
        Assert.False(restored.IsRunning);
        restored.Catalog.NewTaskCommand.Execute(null);
        await fixture.WaitUntilAsync(() => restored.ComposerText == "取消时新输入的草稿也必须保留。");
        Assert.Empty(fixture.Server.Requests);
    }
}
