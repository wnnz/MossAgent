using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationFollowupTests
{
    [Fact]
    public async Task EnqueueConsumesOnlyTheMatchingDraftAndPreservesTheRunningTask()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var task = fixture.Task with { Status = AgentTaskStatus.Running };
        await fixture.Repository.SaveTaskAsync(task, fixture.Token);
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(task.ProjectId, task.Id);
        Assert.True(await drafts.SaveAsync([new(scope, "queued input", 0)], fixture.Token));
        var queued = ConversationFollowupTestData.Create(task);
        var initial = await repository.GetAsync(task.Id, fixture.Token);
        Assert.Equal(0, initial.Revision);
        Assert.Empty(initial.Items);
        Assert.True(await repository.SaveAsync(new(task.Id, 0, [queued]), [new(scope, "", 1)], fixture.Token));
        Assert.Equal(new("", 2), await drafts.GetAsync(scope, fixture.Token));
        Assert.Equal(AgentTaskStatus.Running, Assert.Single(await fixture.Repository.GetTasksAsync(task.ProjectId, fixture.Token)).Status);
        Assert.Empty(await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token));
        var restored = await new SqliteConversationFollowupRepository(fixture.Connections).GetAsync(task.Id, fixture.Token);
        Assert.Equal(1, restored.Revision);
        var item = Assert.Single(restored.Items);
        Assert.Equal(queued.Id, item.Id);
        Assert.Equal(queued.Model, item.Model);
        Assert.Equal(queued.AgentInstructions, item.AgentInstructions);
        Assert.Equal(queued.AgentProfileId, item.AgentProfileId);
        Assert.Equal(queued.AuthorizedRoots, item.AuthorizedRoots);
        Assert.Equal(queued.ApprovalPolicy, item.ApprovalPolicy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EitherRevisionConflictRollsBackQueueAndDraftTogether(bool queueConflict)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        Assert.True(await drafts.SaveAsync([new(scope, "retained", 0)], fixture.Token));
        var first = ConversationFollowupTestData.Create(fixture.Task, "first");
        if (queueConflict) Assert.True(await repository.SaveAsync(new(fixture.Task.Id, 0, [first]), [], fixture.Token));
        var second = ConversationFollowupTestData.Create(fixture.Task, "second");
        Assert.False(await repository.SaveAsync(new(fixture.Task.Id, 0, [second]),
            [new(scope, "", queueConflict ? 1 : 0)], fixture.Token));
        var queue = await repository.GetAsync(fixture.Task.Id, fixture.Token);
        Assert.Equal(queueConflict ? 1 : 0, queue.Revision);
        Assert.Equal(queueConflict ? new[] { first.Id } : [], queue.Items.Select(item => item.Id));
        Assert.Equal(new("retained", 1), await drafts.GetAsync(scope, fixture.Token));
    }

    [Fact]
    public async Task EditingReorderingAndDeletionAreRevisionCheckedAcrossInstances()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var other = new SqliteConversationFollowupRepository(fixture.Connections);
        var first = ConversationFollowupTestData.Create(fixture.Task, "first");
        var second = ConversationFollowupTestData.Create(fixture.Task, "second");
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, 0, [first, second]), [], fixture.Token));
        var edited = second with { Prompt = "edited", Model = second.Model with { ReasoningEffort = "high" } };
        Assert.True(await other.SaveAsync(new(fixture.Task.Id, 1, [edited, first]), [], fixture.Token));
        Assert.False(await repository.SaveAsync(new(fixture.Task.Id, 1, []), [], fixture.Token));
        var restored = await repository.GetAsync(fixture.Task.Id, fixture.Token);
        Assert.Equal(new[] { second.Id, first.Id }, restored.Items.Select(item => item.Id));
        Assert.Equal("edited", restored.Items[0].Prompt);
        Assert.Equal("high", restored.Items[0].Model.ReasoningEffort);
        Assert.True(await repository.SaveAsync(restored with { Items = [] }, [], fixture.Token));
        Assert.Equal(3, (await other.GetAsync(fixture.Task.Id, fixture.Token)).Revision);
        Assert.False(await repository.SaveAsync(new(fixture.Task.Id, 0, [first]), [], fixture.Token));
    }

    [Fact]
    public async Task TwoWritersCannotBothConsumeTheSameQueueRevision()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var first = new SqliteConversationFollowupRepository(fixture.Connections);
        var second = new SqliteConversationFollowupRepository(fixture.Connections);
        var results = await Task.WhenAll(first.SaveAsync(new(fixture.Task.Id, 0,
            [ConversationFollowupTestData.Create(fixture.Task, "first")]), [], fixture.Token),
            second.SaveAsync(new(fixture.Task.Id, 0, [ConversationFollowupTestData.Create(fixture.Task, "second")]), [], fixture.Token));
        Assert.Single(results, result => result);
        Assert.Single((await first.GetAsync(fixture.Task.Id, fixture.Token)).Items);
    }
}
