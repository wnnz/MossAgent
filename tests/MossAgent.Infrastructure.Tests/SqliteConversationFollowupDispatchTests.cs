using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationFollowupDispatchTests
{
    [Fact]
    public async Task DispatchCommitsTheFirstMessageStatusAndQueueRevisionTogether()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var first = ConversationFollowupTestData.Create(fixture.Task, "  first  ");
        var second = ConversationFollowupTestData.Create(fixture.Task, "second");
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, 0, [first, second]), [], fixture.Token));
        var prepared = await ConversationFollowupDispatchTestData.PrepareAsync(fixture, fixture.Task, first);
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        Assert.True(await drafts.SaveAsync([new(scope, "foreground draft", 0)], fixture.Token));
        Assert.Null(await repository.DispatchAsync(prepared with { Followup = second }, fixture.Token));
        Assert.Null(await repository.DispatchAsync(prepared with { QueueRevision = 2 }, fixture.Token));
        var dispatched = Assert.IsType<ConversationFollowupDispatch>(await repository.DispatchAsync(prepared, fixture.Token));
        Assert.Equal(AgentTaskStatus.Running, dispatched.Task.Status);
        Assert.Null(dispatched.Task.FailureReason);
        Assert.Equal(first.ApprovalPolicy, dispatched.Task.ApprovalPolicy);
        Assert.Equal(first.Model, dispatched.Followup.Model);
        Assert.Equal(first.AgentInstructions, dispatched.Followup.AgentInstructions);
        Assert.Equal(first.Id, dispatched.Message.Id);
        Assert.Equal("first", dispatched.Message.Content);
        Assert.Equal(2, dispatched.QueueRevision);
        Assert.Equal(dispatched.Message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        var remaining = await repository.GetAsync(fixture.Task.Id, fixture.Token);
        Assert.Equal(2, remaining.Revision);
        Assert.Equal(second.Id, Assert.Single(remaining.Items).Id);
        Assert.Equal(new("foreground draft", 1), await drafts.GetAsync(scope, fixture.Token));
        Assert.Null(await repository.DispatchAsync(prepared, fixture.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(remaining with { Items = [first, second] }, [], fixture.Token));
        Assert.Equal(second.Id, Assert.Single((await repository.GetAsync(fixture.Task.Id, fixture.Token)).Items).Id);
    }

    [Fact]
    public async Task AMessageWriteFailureRollsBackClaimStatusAndFailureReason()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var failed = fixture.Task with { FailureReason = TaskFailureReason.Unknown };
        await fixture.Repository.SaveTaskAsync(failed, fixture.Token);
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var queued = ConversationFollowupTestData.Create(failed);
        Assert.True(await repository.SaveAsync(new(failed.Id, 0, [queued]), [], fixture.Token));
        var prepared = await ConversationFollowupDispatchTestData.PrepareAsync(fixture, failed, queued);
        await fixture.ExecuteAsync("CREATE TRIGGER reject_followup_message BEFORE INSERT ON messages BEGIN SELECT RAISE(ABORT,'synthetic message failure'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => repository.DispatchAsync(prepared, fixture.Token));
        var restored = Assert.Single(await fixture.Repository.GetTasksAsync(failed.ProjectId, fixture.Token));
        Assert.Equal(failed, restored);
        Assert.Empty(await fixture.Repository.GetMessagesAsync(failed.Id, fixture.Token));
        var queue = await repository.GetAsync(failed.Id, fixture.Token);
        Assert.Equal(1, queue.Revision);
        Assert.Equal(queued.Id, Assert.Single(queue.Items).Id);
        await fixture.ExecuteAsync("DROP TRIGGER reject_followup_message;");
        Assert.NotNull(await repository.DispatchAsync(prepared, fixture.Token));
    }

    [Fact]
    public async Task ConcurrentDispatchAndRestartCannotReplayACommittedMessage()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var first = new SqliteConversationFollowupRepository(fixture.Connections);
        var other = new SqliteConversationFollowupRepository(fixture.Connections);
        var queued = ConversationFollowupTestData.Create(fixture.Task);
        var next = ConversationFollowupTestData.Create(fixture.Task, "requires manual continuation");
        Assert.True(await first.SaveAsync(new(fixture.Task.Id, 0, [queued, next]), [], fixture.Token));
        var prepared = await ConversationFollowupDispatchTestData.PrepareAsync(fixture, fixture.Task, queued);
        var results = await Task.WhenAll(first.DispatchAsync(prepared, fixture.Token),
            other.DispatchAsync(prepared, fixture.Token));
        Assert.Single(results, result => result is not null);
        Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        var restored = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Task.ProjectId, fixture.Token));
        Assert.Equal(AgentTaskStatus.Interrupted, restored.Status);
        Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        var queue = await other.GetAsync(fixture.Task.Id, fixture.Token);
        Assert.Equal(2, queue.Revision);
        Assert.Equal(next.Id, Assert.Single(queue.Items).Id);
        Assert.Null(await first.DispatchAsync(prepared, fixture.Token));
    }

    [Theory]
    [InlineData("active")]
    [InlineData("changed")]
    [InlineData("archived")]
    [InlineData("foreign")]
    public async Task StaleOrUnavailableTasksCannotBeDispatched(string kind)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var item = ConversationFollowupTestData.Create(fixture.Task);
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, 0, [item]), [], fixture.Token));
        var expected = fixture.Task;
        if (kind == "active") await fixture.Repository.SaveTaskAsync(expected with { Status = AgentTaskStatus.Running }, fixture.Token);
        if (kind == "changed") await fixture.Repository.SaveTaskAsync(expected with { Title = "external edit" }, fixture.Token);
        if (kind == "archived") await fixture.Repository.SetTaskArchivedAsync(expected.Id, true, fixture.Token);
        if (kind == "foreign") expected = expected with { ProjectId = Guid.NewGuid() };
        var prepared = await ConversationFollowupDispatchTestData.PrepareAsync(fixture, expected, item);
        Assert.Null(await repository.DispatchAsync(prepared, fixture.Token));
        Assert.Equal(item.Id, Assert.Single((await repository.GetAsync(fixture.Task.Id, fixture.Token)).Items).Id);
        Assert.Empty(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
    }
}
