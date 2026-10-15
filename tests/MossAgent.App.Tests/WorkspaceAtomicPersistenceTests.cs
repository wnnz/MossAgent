using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAtomicPersistenceTests
{
    [Fact]
    public async Task RejectedInitialMessageDoesNotLeaveAnOrphanRunningTask()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.RejectMessageAsync("rejected prompt");

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Factory.CreateAsync(
            fixture.Project, "rejected prompt", ApprovalPolicy.ReadOnly, false, fixture.Token));

        Assert.Equal(fixture.Task.Id, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).Id);
    }

    [Fact]
    public async Task RejectedResumePreservesPreviousStatusAndConversation()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var previous = await fixture.Factory.CompleteAsync(fixture.Task, "previous reply", AgentTaskStatus.Completed, fixture.Token);
        await fixture.RejectMessageAsync("rejected prompt");

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Factory.ResumeAsync(
            previous, "rejected prompt", ApprovalPolicy.FullAccess, fixture.Token));

        var saved = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(AgentTaskStatus.Completed, saved.Status);
        Assert.Equal(ApprovalPolicy.ReadOnly, saved.ApprovalPolicy);
        Assert.Equal(new[] { "initial prompt", "previous reply" },
            (await fixture.Repository.GetMessagesAsync(previous.Id, fixture.Token)).Select(message => message.Content));
    }

    [Fact]
    public async Task RejectedTerminalStatusDoesNotAppendAnUncommittedAssistantReply()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.RejectCompletionAsync();

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Factory.CompleteAsync(
            fixture.Task, "final streamed reply", AgentTaskStatus.Completed, fixture.Token));

        Assert.Equal(AgentTaskStatus.Running, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).Status);
        Assert.Equal(MessageRole.User, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)).Role);
    }

    [Fact]
    public async Task RejectedBatchRollsBackEarlierMessagesAndTaskChanges()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.RejectMessageAsync("rejected prompt");
        var updated = fixture.Task with { Title = "must roll back", Status = AgentTaskStatus.Completed };
        var now = DateTimeOffset.UtcNow;
        ConversationMessage[] batch =
        [
            new(Guid.NewGuid(), updated.Id, MessageRole.Assistant, "earlier segment", now),
            new(Guid.NewGuid(), updated.Id, MessageRole.User, "rejected prompt", now)
        ];

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.SaveTaskWithMessagesAsync(updated, batch, fixture.Token));

        var stored = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(fixture.Task.Title, stored.Title);
        Assert.Equal(AgentTaskStatus.Running, stored.Status);
        Assert.Single(await fixture.Repository.GetMessagesAsync(updated.Id, fixture.Token));
    }

    [Fact]
    public async Task CrossTaskMessageIsRejectedBeforeAnyWrite()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var other = await fixture.Factory.CreateAsync(fixture.Project, "other task", ApprovalPolicy.ReadOnly, false, fixture.Token);
        var message = new ConversationMessage(Guid.NewGuid(), other.Id, MessageRole.Assistant, "cross-task content", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.SaveTaskWithMessagesAsync(
            fixture.Task with { Status = AgentTaskStatus.Completed }, [message], fixture.Token));

        Assert.Equal(AgentTaskStatus.Running, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token),
            task => task.Id == fixture.Task.Id).Status);
        Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Single(await fixture.Repository.GetMessagesAsync(other.Id, fixture.Token));
    }

    [Fact]
    public async Task CancelledCommitDoesNotChangeTaskOrMessages()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.Assistant, "not committed", DateTimeOffset.UtcNow);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Repository.SaveTaskWithMessagesAsync(
            fixture.Task with { Status = AgentTaskStatus.Completed }, [message], cancellation.Token));

        Assert.Equal(AgentTaskStatus.Running, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).Status);
        Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
    }

    [Fact]
    public async Task CommittedBatchPreservesInsertionOrderForEqualTimestamps()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        ConversationMessage[] messages =
        [
            new(Guid.NewGuid(), fixture.Task.Id, MessageRole.Assistant, "before tool", now),
            new(Guid.NewGuid(), fixture.Task.Id, MessageRole.Tool, "tool result", now, "call-1"),
            new(Guid.NewGuid(), fixture.Task.Id, MessageRole.Assistant, "after tool", now)
        ];

        await fixture.Repository.SaveTaskWithMessagesAsync(fixture.Task with { Status = AgentTaskStatus.Completed }, messages, fixture.Token);

        Assert.Equal(new[] { "initial prompt", "before tool", "tool result", "after tool" },
            (await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)).Select(message => message.Content));
        Assert.Equal(AgentTaskStatus.Completed, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).Status);
    }
}
