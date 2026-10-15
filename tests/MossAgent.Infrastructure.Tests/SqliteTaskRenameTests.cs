using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteTaskRenameTests
{
    [Theory]
    [InlineData("  new title  ", "new title")]
    [InlineData("quoted ' title; DROP TABLE agent_tasks; --", "quoted ' title; DROP TABLE agent_tasks; --")]
    public async Task Rename_ChangesOnlyTitleAndTimestamp(string title, string expectedTitle)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var task = fixture.Task with
        {
            ApprovalPolicy = ApprovalPolicy.FullAccess,
            FailureReason = TaskFailureReason.ProviderTimeout,
            WorktreePath = Path.Combine(fixture.Project.PrimaryDirectory, "worktree")
        };
        var message = new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.Assistant,
            "retained reply", DateTimeOffset.UtcNow);
        await fixture.Repository.SaveTaskWithMessagesAsync(task, [message], fixture.Token);
        await fixture.Repository.SetTaskPinnedAsync(task.Id, true, fixture.Token);
        var before = Assert.Single(await fixture.Repository.GetTasksAsync(task.ProjectId, fixture.Token));

        var renamed = await fixture.Repository.RenameTaskAsync(task, title, fixture.Token);

        Assert.NotNull(renamed);
        Assert.Equal(before with { Title = expectedTitle, UpdatedAt = renamed.UpdatedAt }, renamed);
        Assert.Equal(renamed, Assert.Single(await fixture.Repository.GetTasksAsync(task.ProjectId, fixture.Token)));
        Assert.Equal(renamed, Assert.Single(await fixture.Repository.GetPinnedTasksAsync(fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token)));
    }

    [Theory]
    [InlineData(AgentTaskStatus.Draft)]
    [InlineData(AgentTaskStatus.Completed)]
    [InlineData(AgentTaskStatus.Failed)]
    [InlineData(AgentTaskStatus.Cancelled)]
    [InlineData(AgentTaskStatus.Interrupted)]
    public async Task EligibleStatus_IsPreserved(AgentTaskStatus status)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.Repository.SaveTaskAsync(fixture.Task with { Status = status }, fixture.Token);
        var before = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));

        var renamed = await fixture.Repository.RenameTaskAsync(before, "renamed", fixture.Token);

        Assert.NotNull(renamed);
        Assert.Equal(before with { Title = "renamed", UpdatedAt = renamed.UpdatedAt }, renamed);
    }

    [Theory]
    [InlineData(AgentTaskStatus.Running)]
    [InlineData(AgentTaskStatus.WaitingForApproval)]
    public async Task ActiveTask_IsNotChanged(AgentTaskStatus status)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.Repository.SaveTaskAsync(fixture.Task with { Status = status }, fixture.Token);
        var before = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));

        Assert.Null(await fixture.Repository.RenameTaskAsync(fixture.Task, "renamed", fixture.Token));
        Assert.Equal(before, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("project")]
    [InlineData("title")]
    public async Task MismatchedTarget_IsNotChangedOrCreated(string mismatch)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var before = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));
        var expected = mismatch switch
        {
            "missing" => before with { Id = Guid.NewGuid() },
            "project" => before with { ProjectId = Guid.NewGuid() },
            _ => before with { Title = "stale title" }
        };

        Assert.Null(await fixture.Repository.RenameTaskAsync(expected, "renamed", fixture.Token));
        Assert.Equal(before, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM agent_tasks;"));
    }

    [Fact]
    public async Task ArchivedTask_RemainsArchivedAndUnchanged()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.Repository.SetTaskArchivedAsync(fixture.Task.Id, true, fixture.Token);
        var before = Assert.Single(await fixture.Repository.GetArchivedTasksAsync(fixture.Project.Id, fixture.Token));

        Assert.Null(await fixture.Repository.RenameTaskAsync(before, "renamed", fixture.Token));
        Assert.Empty(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(before, Assert.Single(await fixture.Repository.GetArchivedTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyTitle_IsRejectedWithoutChangingTask(string title)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var before = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.RenameTaskAsync(before, title, fixture.Token));
        Assert.Equal(before, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    [Fact]
    public async Task PreCanceledRename_DoesNotChangeTask()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var before = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Repository.RenameTaskAsync(before, "renamed", cancellation.Token));
        Assert.Equal(before, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    [Fact]
    public async Task StorageFailure_RollsBackTaskWithoutChangingRelatedData()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var task = fixture.Task with { FailureReason = TaskFailureReason.ProviderTimeout };
        var message = new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.Assistant,
            "retained reply", DateTimeOffset.UtcNow);
        await fixture.Repository.SaveTaskWithMessagesAsync(task, [message], fixture.Token);
        await fixture.Repository.SetTaskPinnedAsync(task.Id, true, fixture.Token);
        var before = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_rename AFTER UPDATE OF title ON agent_tasks
            BEGIN SELECT RAISE(ABORT, 'synthetic refusal'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.RenameTaskAsync(before, "renamed", fixture.Token));
        Assert.Equal(before, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Equal(before, Assert.Single(await fixture.Repository.GetPinnedTasksAsync(fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token)));
        Assert.Equal((long)TaskFailureReason.ProviderTimeout, await fixture.CountAsync("SELECT reason FROM agent_task_failures;"));
    }
}
