using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorktreeObjectRuntimeTests
{
    [Fact]
    public async Task CopiedGitMetadataCannotAuthorizeReplacementAndRestoringOriginalRestoresAccess()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var task = await CreateTaskAsync(owner);
        var path = task.WorktreePath!;
        var original = path + "-original";
        await RootObjectTestDirectory.MoveWhenReadersFinishAsync(path, original, fixture.Token);
        Directory.CreateDirectory(path);
        File.Copy(Path.Combine(original, ".git"), Path.Combine(path, ".git"));
        await File.WriteAllTextAsync(Path.Combine(path, "README.md"), "replacement-secret", fixture.Token);
        // The pre-existing Git/path checks accept this copied registration; persisted object identity must reject it.
        await new GitWorktreeService(new AppDataPaths(fixture.DataRoot), fixture.Git)
            .ValidateAsync(owner.Project, task.Id, path, fixture.Token);
        await using var reopened = AppComposition.CreateServices(fixture.DataRoot);
        var context = reopened.GetRequiredService<WorkspaceTaskPanelCoordinator>().CreateContext(task, owner.Project);
        var tools = reopened.GetRequiredService<IToolExecutor>();
        var refused = await tools.ExecuteAsync(new("substituted-read", "filesystem.read_file",
            JsonSerializer.SerializeToElement(new { path = "README.md" })), context, fixture.Token);
        Assert.Equal("workspace_denied", refused.ErrorCode);
        Assert.DoesNotContain("replacement-secret", refused.Content ?? string.Empty);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(() => fixture.Worktrees.RemoveAsync(owner.Project, path, fixture.Token));
        var messages = await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(() => reopened.GetRequiredService<WorkspaceTaskFactory>()
            .ResumeAsync(task, "must not be saved", ApprovalPolicy.ReadOnly, fixture.Token));
        Assert.Equal(messages.Count, (await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token)).Count);
        Assert.True(File.Exists(Path.Combine(path, "README.md")));
        File.Delete(Path.Combine(path, "README.md"));
        File.Delete(Path.Combine(path, ".git"));
        Directory.Delete(path);
        await RootObjectTestDirectory.MoveWhenReadersFinishAsync(original, path, fixture.Token);
        var restored = await tools.ExecuteAsync(new("original-read", "filesystem.read_file",
            JsonSerializer.SerializeToElement(new { path = "README.md" })), context, fixture.Token);
        Assert.True(restored.IsSuccess, restored.Summary);
        Assert.Contains("committed baseline", restored.Content);
        var audits = await reopened.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(task.Id, 10, fixture.Token);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit => Assert.NotNull(audit.CompletedAt));
        Assert.Empty(fixture.Server.Requests);
    }

    [Fact]
    public async Task ComposedTaskLeasesPinTheirOwnWorktreeAndReleaseIndependently()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var first = await CreateTaskAsync(owner);
        var second = await CreateTaskAsync(owner);
        var firstContext = fixture.Panels.CreateContext(first, owner.Project);
        var secondContext = fixture.Panels.CreateContext(second, owner.Project);
        await using var firstLease = await Assert.IsAssignableFrom<ITaskWorkspaceLeaseProvider>(firstContext.WorkspaceGuard)
            .AcquireAsync(firstContext, fixture.Token);
        await using var secondLease = await Assert.IsAssignableFrom<ITaskWorkspaceLeaseProvider>(secondContext.WorkspaceGuard)
            .AcquireAsync(secondContext, fixture.Token);
        Assert.Throws<IOException>(() => Directory.Move(first.WorktreePath!, first.WorktreePath + "-moved"));
        Assert.Throws<IOException>(() => Directory.Move(second.WorktreePath!, second.WorktreePath + "-moved"));
        await firstLease.DisposeAsync();
        await firstLease.DisposeAsync();
        await RootObjectTestDirectory.MoveWhenReadersFinishAsync(first.WorktreePath!, first.WorktreePath + "-moved", fixture.Token);
        Assert.Throws<IOException>(() => Directory.Move(second.WorktreePath!, second.WorktreePath + "-moved"));
        Directory.Move(first.WorktreePath + "-moved", first.WorktreePath!);
    }

    [Fact]
    public async Task CancellingActualModelStreamReleasesWorktreeLeaseWithoutReplayingOnReopen()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync(pauseResponse: true);
        var fixture = owner.Fixture;
        var workspace = fixture.Workspace;
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var task = workspace.Catalog.SelectedTask!;
        Assert.Throws<IOException>(() => Directory.Move(task.WorktreePath!, task.WorktreePath + "-moved"));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        await fixture.AssertPersistedAsync(AgentTaskStatus.Cancelled);
        await RootObjectTestDirectory.MoveWhenReadersFinishAsync(task.WorktreePath!, task.WorktreePath + "-moved", fixture.Token);
        Directory.Move(task.WorktreePath + "-moved", task.WorktreePath!);
        await using var reopened = AppComposition.CreateServices(fixture.DataRoot);
        var restored = reopened.GetRequiredService<MainWindowViewModel>().Workspace;
        await restored.LoadAsync();
        Assert.Equal(AgentTaskStatus.Cancelled, Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token)).Status);
        Assert.Single(fixture.Server.Requests);
        Assert.False(restored.IsRunning);
    }

    internal static async Task<AgentTask> CreateTaskAsync(WorkspaceWorktreeGitFixture owner, bool legacy = false,
        ApprovalPolicy policy = ApprovalPolicy.ReadOnly)
    {
        var fixture = owner.Fixture;
        var id = Guid.NewGuid();
        var worktrees = legacy ? new GitWorktreeService(new AppDataPaths(fixture.DataRoot), fixture.Git) : fixture.Worktrees;
        var path = await worktrees.CreateAsync(owner.Project, id, fixture.Token);
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(id, owner.Project.Id, "worktree object fixture", policy, AgentTaskStatus.Interrupted, now, now, path);
        await fixture.Tasks.SaveTaskWithMessagesAsync(task,
            [new ConversationMessage(Guid.NewGuid(), id, MessageRole.User, "saved worktree prompt", now)], fixture.Token);
        return task;
    }
}
