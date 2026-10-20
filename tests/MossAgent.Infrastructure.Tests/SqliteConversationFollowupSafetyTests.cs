using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationFollowupSafetyTests
{
    [Theory]
    [InlineData("foreign-task")]
    [InlineData("duplicate")]
    [InlineData("count")]
    [InlineData("empty")]
    [InlineData("long-prompt")]
    [InlineData("foreign-attachment")]
    [InlineData("image-model")]
    [InlineData("null-model")]
    [InlineData("budget")]
    public async Task InvalidSnapshotsLeaveTheQueueUnchanged(string kind)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var item = ConversationFollowupTestData.Create(fixture.Task);
        ConversationFollowup[] items = [item];
        if (kind == "foreign-task") items = [item with { TaskId = Guid.NewGuid() }];
        if (kind == "duplicate") items = [item, item];
        if (kind == "count") items = Enumerable.Range(0, 17).Select(_ => item with { Id = Guid.NewGuid() }).ToArray();
        if (kind == "empty") items = [item with { Prompt = "   " }];
        if (kind == "long-prompt") items = [item with { Prompt = new('x', ConversationDraft.MaximumTextLength + 1) }];
        if (kind == "null-model") items = [item with { Model = null! }];
        if (kind == "budget") items = Enumerable.Range(0, 16).Select(_ => item with { Id = Guid.NewGuid(), Prompt = new('x', ConversationDraft.MaximumTextLength) }).ToArray();
        if (kind is "foreign-attachment" or "image-model")
            items = [item with { Attachments = [new(Guid.NewGuid(), kind == "foreign-attachment" ? Guid.NewGuid() : item.TaskId,
                item.Id, "fixture.png", 10, new('A', 64), "image/png")] }];
        if (kind == "foreign-attachment")
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SaveAsync(new(fixture.Task.Id, 0, items), [], fixture.Token));
        else
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(new(fixture.Task.Id, 0, items), [], fixture.Token));
        Assert.Empty((await repository.GetAsync(fixture.Task.Id, fixture.Token)).Items);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_followup_queues;"));
    }

    [Theory]
    [InlineData("roots")]
    [InlineData("policy")]
    [InlineData("worktree")]
    public async Task ProjectlessEntriesCannotAcquireAnEnvironment(string kind)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var task = fixture.Task with { Id = Guid.NewGuid(), ProjectId = null };
        await fixture.Repository.SaveTaskAsync(task, fixture.Token);
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var item = ConversationFollowupTestData.Create(task);
        var invalid = kind switch
        {
            "roots" => item with { AuthorizedRoots = ["D:\\"] },
            "policy" => item with { ApprovalPolicy = ApprovalPolicy.FullAccess },
            _ => item with { WorktreePath = "D:\\worktree" }
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SaveAsync(new(task.Id, 0, [invalid]), [], fixture.Token));
        Assert.True(await repository.SaveAsync(new(task.Id, 0, [item]), [], fixture.Token));
        var prepared = await ConversationFollowupDispatchTestData.PrepareAsync(fixture, task, item);
        var dispatched = Assert.IsType<ConversationFollowupDispatch>(await repository.DispatchAsync(prepared, fixture.Token));
        Assert.Null(dispatched.Task.ProjectId);
        Assert.Equal(ApprovalPolicy.ReadOnly, dispatched.Task.ApprovalPolicy);
        Assert.Empty(dispatched.Followup.AuthorizedRoots);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("project")]
    [InlineData("worktree")]
    [InlineData("archived")]
    public async Task MissingOrMismatchedTaskScopesCannotCreateAQueue(string kind)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var task = kind == "missing" ? fixture.Task with { Id = Guid.NewGuid() } : fixture.Task;
        var item = ConversationFollowupTestData.Create(task);
        if (kind == "project") item = item with { ProjectId = Guid.NewGuid() };
        if (kind == "worktree") item = item with { WorktreePath = "D:\\foreign-worktree" };
        if (kind == "archived") await fixture.Repository.SetTaskArchivedAsync(task.Id, true, fixture.Token);
        Assert.False(await repository.SaveAsync(new(task.Id, 0, [item]), [], fixture.Token));
        Assert.Equal(0, (await repository.GetAsync(task.Id, fixture.Token)).Revision);
    }

    [Fact]
    public async Task CancellationCannotConsumeADraftOrClaimAQueuedMessage()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var item = ConversationFollowupTestData.Create(fixture.Task);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        Assert.True(await drafts.SaveAsync([new(scope, "retained", 0)], fixture.Token));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync(new(fixture.Task.Id, 0, [item]),
            [new(scope, "", 1)], cancellation.Token));
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, 0, [item]), [], fixture.Token));
        var prepared = await ConversationFollowupDispatchTestData.PrepareAsync(fixture, fixture.Task, item);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.DispatchAsync(prepared, cancellation.Token));
        Assert.Equal(item.Id, Assert.Single((await repository.GetAsync(fixture.Task.Id, fixture.Token)).Items).Id);
        Assert.Empty(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(new("retained", 1), await drafts.GetAsync(scope, fixture.Token));
    }
}
