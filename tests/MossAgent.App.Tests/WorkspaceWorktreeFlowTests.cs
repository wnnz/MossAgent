using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceWorktreeFlowTests
{
    [Fact]
    public async Task SelectedWorktree_SendsFollowUpRestoresHistoryAndUsesIsolatedToolRoots()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var workspace = fixture.Workspace;
        var source = Path.Combine(owner.Project.PrimaryDirectory, "README.md");
        await File.WriteAllTextAsync(source, "uncommitted source change\n", fixture.Token);
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
        var task = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        var path = Assert.IsType<string>(task.WorktreePath);
        Assert.Equal("committed baseline\n", (await File.ReadAllTextAsync(Path.Combine(path, "README.md"), fixture.Token)).ReplaceLineEndings("\n"));
        Assert.Equal("uncommitted source change\n", await File.ReadAllTextAsync(source, fixture.Token));
        Assert.Equal($"mossagent/{task.Id:N}", (await owner.GitAsync(["branch", "--show-current"], path)).StandardOutput.Trim());
        Assert.True(workspace.Catalog.Environment.IsWorktree);
        Assert.False(workspace.Catalog.Environment.CanChoose);
        await AssertToolBoundariesAsync(owner, task);

        workspace.ComposerText = "在同一个工作树中继续。";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        Assert.Equal(task.Id, workspace.Catalog.SelectedTask?.Id);
        Assert.Equal(path, workspace.Catalog.SelectedTask?.WorktreePath);
        Assert.Equal(2, (await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token)).Count(message => message.Role == MessageRole.User));
        Assert.Single(Directory.GetDirectories(Path.GetDirectoryName(path)!));
        Assert.Equal(2, fixture.Server.Requests.Count);
        await using var reopened = AppComposition.CreateServices(fixture.DataRoot);
        var restored = reopened.GetRequiredService<MainWindowViewModel>().Workspace;
        await restored.LoadAsync();
        restored.Catalog.SelectedTask = Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
        await fixture.WaitUntilAsync(() => restored.Messages.Count >= 4);
        Assert.Equal(path, restored.Catalog.SelectedTask?.WorktreePath);
        Assert.True(restored.Catalog.Environment.IsWorktree);
        Assert.False(restored.Catalog.Environment.CanChoose);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.Contains("README.md", (await owner.GitAsync(["status", "--short", "--", "README.md"])).StandardOutput);
    }

    [Fact]
    public async Task CancellingConversation_PreservesWorktreeAndDirtyCleanupRequiresExplicitConfirmation()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync(pauseResponse: true);
        var fixture = owner.Fixture;
        var workspace = fixture.Workspace;
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var task = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        var path = Assert.IsType<string>(task.WorktreePath);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        await fixture.AssertPersistedAsync(AgentTaskStatus.Cancelled);
        Assert.True(Directory.Exists(path));
        var dirty = Path.Combine(path, "unsaved.txt");
        await File.WriteAllTextAsync(dirty, "keep this change", fixture.Token);
        await workspace.Catalog.CleanupWorktreeCommand.ExecuteAsync(null);
        Assert.True(File.Exists(dirty));
        await workspace.Catalog.CleanupWorktreeCommand.ExecuteAsync(null);
        Assert.True(File.Exists(dirty));
        Assert.Equal(path, workspace.Catalog.SelectedTask?.WorktreePath);
        Assert.Equal(WorkspaceText.Get("TaskEnvironmentCleanupFailure"), workspace.Catalog.Status);
        Assert.DoesNotContain(path, workspace.Catalog.Status);
        Assert.Equal(path, Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token)).WorktreePath);
        File.Delete(dirty);
        await workspace.Catalog.CleanupWorktreeCommand.ExecuteAsync(null);
        Assert.True(Directory.Exists(path));
        await workspace.Catalog.CleanupWorktreeCommand.ExecuteAsync(null);
        Assert.False(Directory.Exists(path));
        Assert.Null(workspace.Catalog.SelectedTask?.WorktreePath);
        Assert.Null(Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token)).WorktreePath);
        Assert.NotEmpty(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
    }

    private static async Task AssertToolBoundariesAsync(WorkspaceWorktreeGitFixture owner, AgentTask task)
    {
        var fixture = owner.Fixture;
        var context = fixture.Panels.CreateContext(task, owner.Project);
        Assert.Equal(task.WorktreePath, context.WorkingDirectory);
        Assert.Contains(task.WorktreePath!, context.AuthorizedRoots);
        Assert.DoesNotContain(owner.Project.PrimaryDirectory, context.AuthorizedRoots);
        Assert.Equal(ApprovalPolicy.ReadOnly, context.ApprovalPolicy);
        var read = await fixture.Tools.ExecuteAsync(new("read-worktree", "filesystem.read_file",
            JsonSerializer.SerializeToElement(new { path = "README.md" })), context, fixture.Token);
        Assert.True(read.IsSuccess, read.Summary);
        Assert.Contains("committed baseline", read.Content);
        var escaped = await fixture.Tools.ExecuteAsync(new("read-source", "filesystem.read_file",
            JsonSerializer.SerializeToElement(new { path = Path.Combine(owner.Project.PrimaryDirectory, "README.md") })), context, fixture.Token);
        Assert.False(escaped.IsSuccess);
        var denied = await fixture.Tools.ExecuteAsync(new("write-worktree", "filesystem.write_file",
            JsonSerializer.SerializeToElement(new { path = "must-not-exist.txt", content = "denied" })), context, fixture.Token);
        Assert.False(denied.IsSuccess);
        Assert.False(File.Exists(Path.Combine(task.WorktreePath!, "must-not-exist.txt")));
    }

    [Fact]
    public async Task SqliteRejectingFirstMessage_RollsBackTaskCleansWorktreeAndPreservesDraft()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        await using (var connection = new SqliteConnectionFactory(new AppDataPaths(fixture.DataRoot)).Create())
        {
            await connection.OpenAsync(fixture.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER reject_worktree_message BEFORE INSERT ON messages
                WHEN NEW.content='rejected worktree prompt'
                BEGIN SELECT RAISE(ABORT, 'synthetic worktree refusal'); END;
                """;
            await command.ExecuteNonQueryAsync(fixture.Token);
        }
        var workspace = fixture.Workspace;
        workspace.ComposerText = "rejected worktree prompt";
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.HasSendPreparationError);
        Assert.False(workspace.IsRunning);
        Assert.Equal("rejected worktree prompt", workspace.ComposerText);
        Assert.Null(workspace.Catalog.SelectedTask);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
        Assert.Empty(fixture.Server.Requests);
        Assert.False(workspace.Catalog.Environment.HasPreparationNotice);
        var worktrees = (await owner.GitAsync(["worktree", "list", "--porcelain"])).StandardOutput;
        Assert.Single(worktrees.Split('\n'), line => line.StartsWith("worktree ", StringComparison.Ordinal));
        Assert.Empty(Directory.GetDirectories(Path.Combine(fixture.DataRoot, "worktrees")));
    }
}
