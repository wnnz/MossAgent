using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSubmissionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SubmissionAndDraftConsumptionReopenTogether(bool projectless, bool existing)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var task = fixture.Task with { Id = existing ? fixture.Task.Id : Guid.NewGuid(), Status = AgentTaskStatus.Running };
        if (projectless) task = task with { Id = Guid.NewGuid(), ProjectId = null };
        if (existing) await fixture.Repository.SaveTaskAsync(task, fixture.Token);
        var source = new ConversationDraftScope(task.ProjectId, existing ? task.Id : null);
        var target = new ConversationDraftScope(task.ProjectId, task.Id);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        Assert.True(await drafts.SaveAsync([new(source, "sent", 0)], fixture.Token));
        ConversationDraftUpdate[] updates = existing ? [new(source, "next", 1)]
            : [new(source, string.Empty, 1), new(target, "next", 0)];

        await fixture.Repository.SaveSubmissionAsync(task, Message(task), updates, fixture.Token);

        var reopened = new SqliteWorkspaceRepository(fixture.Connections);
        var restored = new SqliteConversationDraftRepository(fixture.Connections);
        Assert.Equal("sent", Assert.Single(await reopened.GetMessagesAsync(task.Id, fixture.Token)).Content);
        Assert.Equal("next", (await restored.GetAsync(target, fixture.Token))!.Text);
        Assert.Equal(existing ? 2 : 1, (await restored.GetAsync(target, fixture.Token))!.Revision);
        if (!existing) Assert.Equal(new(string.Empty, 2), await restored.GetAsync(source, fixture.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictRollsBackTaskMessageAndEveryDraft(bool existing)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var task = fixture.Task with { Id = existing ? fixture.Task.Id : Guid.NewGuid(), Status = AgentTaskStatus.Running };
        var source = new ConversationDraftScope(task.ProjectId, null);
        var target = new ConversationDraftScope(task.ProjectId, task.Id);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        Assert.True(await drafts.SaveAsync([new(source, "original", 0)], fixture.Token));
        if (existing) Assert.True(await drafts.SaveAsync([new(target, "another editor", 0)], fixture.Token));
        var before = await fixture.CountAsync("SELECT COUNT(*) FROM agent_tasks;");

        await Assert.ThrowsAsync<ConversationDraftConflictException>(() => fixture.Repository.SaveSubmissionAsync(task,
            Message(task), [new(source, string.Empty, 1), new(target, "next", existing ? 0 : 9)], fixture.Token));

        Assert.Equal(before, await fixture.CountAsync("SELECT COUNT(*) FROM agent_tasks;"));
        Assert.Empty(await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token));
        Assert.Equal(new("original", 1), await drafts.GetAsync(source, fixture.Token));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        if (existing) Assert.Equal(new("another editor", 1), await drafts.GetAsync(target, fixture.Token));
        else Assert.Null(await drafts.GetAsync(target, fixture.Token));
    }

    [Theory]
    [InlineData("messages")]
    [InlineData("conversation_drafts")]
    public async Task DatabaseFailureLeavesInputAndTaskUnchanged(string table)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        Assert.True(await drafts.SaveAsync([new(scope, "retained", 0)], fixture.Token));
        await fixture.ExecuteAsync($"CREATE TRIGGER reject_submission BEFORE INSERT ON {table} BEGIN SELECT RAISE(ABORT, 'controlled failure'); END;");

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.SaveSubmissionAsync(
            fixture.Task with { Status = AgentTaskStatus.Running }, Message(fixture.Task), [new(scope, string.Empty, 1)], fixture.Token));

        Assert.Empty(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Equal(new("retained", 1), await drafts.GetAsync(scope, fixture.Token));
    }

    [Fact]
    public async Task ConcurrentSubmissionsCannotBothConsumeTheSameDraftRevision()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        var source = new ConversationDraftScope(null, null);
        Assert.True(await drafts.SaveAsync([new(source, "once", 0)], fixture.Token));
        var tasks = Enumerable.Range(0, 2).Select(_ => fixture.Task with { Id = Guid.NewGuid(), ProjectId = null }).ToArray();
        var outcomes = await Task.WhenAll(tasks.Select(async task =>
        {
            try
            {
                await fixture.Repository.SaveSubmissionAsync(task, Message(task),
                    [new(source, string.Empty, 1), new(new(null, task.Id), string.Empty, 0)], fixture.Token);
                return true;
            }
            catch (ConversationDraftConflictException) { return false; }
        }));
        Assert.Single(outcomes, result => result);
        var committed = Assert.Single(await fixture.Repository.GetTasksAsync(null, fixture.Token));
        Assert.Single(await fixture.Repository.GetMessagesAsync(committed.Id, fixture.Token));
        Assert.Equal(new(string.Empty, 2), await drafts.GetAsync(source, fixture.Token));
    }

    [Fact]
    public async Task CancellationAndArchivedTargetsCannotCommitOrConsumeInput()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        Assert.True(await drafts.SaveAsync([new(scope, "retained", 0)], fixture.Token));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Repository.SaveSubmissionAsync(
            fixture.Task, Message(fixture.Task), [new(scope, string.Empty, 1)], cancellation.Token));
        await fixture.Repository.SetTaskArchivedAsync(fixture.Task.Id, true, fixture.Token);
        await Assert.ThrowsAsync<ConversationDraftConflictException>(() => fixture.Repository.SaveSubmissionAsync(
            fixture.Task, Message(fixture.Task), [new(scope, string.Empty, 1)], fixture.Token));
        await fixture.Repository.SetTaskArchivedAsync(fixture.Task.Id, false, fixture.Token);
        Assert.Empty(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(new("retained", 1), await drafts.GetAsync(scope, fixture.Token));
    }

    [Theory]
    [InlineData("foreign-project")]
    [InlineData("foreign-task")]
    [InlineData("missing-target")]
    [InlineData("assistant")]
    [InlineData("duplicate")]
    public async Task InvalidSubmissionCannotMutateAnotherScope(string kind)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var message = Message(fixture.Task);
        ConversationDraftUpdate[] updates = [new(scope, string.Empty, 0)];
        if (kind == "foreign-project") updates = [new(new(null, fixture.Task.Id), string.Empty, 0)];
        if (kind == "foreign-task") updates = [new(scope with { TaskId = Guid.NewGuid() }, string.Empty, 0)];
        if (kind == "missing-target") updates = [new(scope with { TaskId = null }, string.Empty, 0)];
        if (kind == "assistant") message = message with { Role = MessageRole.Assistant };
        if (kind == "duplicate") updates = [updates[0], updates[0]];
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.SaveSubmissionAsync(fixture.Task, message, updates, fixture.Token));
        Assert.Empty(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_drafts;"));
    }

    private static ConversationMessage Message(AgentTask task) =>
        new(Guid.NewGuid(), task.Id, MessageRole.User, "sent", DateTimeOffset.UtcNow);
}
