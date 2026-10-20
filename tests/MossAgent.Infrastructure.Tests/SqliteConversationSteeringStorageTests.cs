using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringStorageTests
{
    [Fact]
    public async Task AcceptanceAndDraftConsumptionAreAtomicAndDistinctFromAppliedMessages()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        Assert.True(fixture.Repository.SupportsDrafts(drafts));
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0)], fixture.Token));
        var id = Guid.NewGuid();
        var ack = Assert.IsType<ConversationSteeringAcceptance>(await fixture.Repository.AcceptAsync(run.TaskId, run.Id, id,
            "guidance", new(scope, string.Empty, 1), fixture.Token));
        Assert.False(ack.AlreadyAccepted);
        Assert.Equal(1, ack.Revision);
        Assert.Equal(ConversationSteeringInputState.Pending, ack.Input.State);
        Assert.Equal(new(string.Empty, 2), await drafts.GetAsync(scope, fixture.Token));
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
        var reopened = new SqliteConversationSteeringRepository(fixture.Connections);
        Assert.Equal(ack.Input, Assert.Single((await reopened.GetInputsAsync(run.TaskId, long.MaxValue, 64, fixture.Token)).Items));
        Assert.True(await drafts.SaveAsync([new(scope, "newer foreground draft", 2)], fixture.Token));
        var duplicate = Assert.IsType<ConversationSteeringAcceptance>(await reopened.AcceptAsync(run.TaskId, run.Id, id,
            "guidance", new(scope, string.Empty, 1), fixture.Token));
        Assert.True(duplicate.AlreadyAccepted);
        Assert.Equal(ack.Input, duplicate.Input);
        Assert.Equal(1, duplicate.Revision);
        Assert.Equal(new("newer foreground draft", 3), await drafts.GetAsync(scope, fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => reopened.AcceptAsync(run.TaskId, run.Id, id, "different", null, fixture.Token));
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("write-failure")]
    public async Task DraftFailureRollsBackReceiptAndRunRevision(string kind)
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        Assert.True(await drafts.SaveAsync([new(scope, "retain me", 0)], fixture.Token));
        if (kind == "write-failure")
            await fixture.ExecuteAsync("CREATE TRIGGER reject_steering_draft BEFORE UPDATE ON conversation_drafts BEGIN SELECT RAISE(ABORT,'synthetic draft failure'); END;");
        var operation = () => fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "guidance",
            new(scope, string.Empty, kind == "conflict" ? 0 : 1), fixture.Token);
        if (kind == "conflict") await Assert.ThrowsAsync<ConversationDraftConflictException>(operation);
        else await Assert.ThrowsAsync<SqliteException>(operation);
        Assert.Empty((await fixture.InputsAsync()).Items);
        Assert.Equal(run, await fixture.ReloadAsync(run));
        Assert.Equal(new("retain me", 1), await drafts.GetAsync(scope, fixture.Token));
    }

    [Fact]
    public async Task PreservesTheNextDraftAndPagesHistoryWithoutCrossTaskResults()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0)], fixture.Token));
        Assert.NotNull(await fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "guidance",
            new(scope, "next draft", 1), fixture.Token));
        await fixture.AcceptAsync(run, "second");
        await fixture.AcceptAsync(run, "third");
        var page = await fixture.Repository.GetInputsAsync(run.TaskId, long.MaxValue, 2, fixture.Token);
        Assert.True(page.HasMore);
        Assert.Equal(new[] { "third", "second" }, page.Items.Select(item => item.Text));
        var next = await fixture.Repository.GetInputsAsync(run.TaskId, page.Items[^1].Sequence, 2, fixture.Token);
        Assert.False(next.HasMore);
        Assert.Equal("guidance", Assert.Single(next.Items).Text);
        Assert.Empty((await fixture.InputsAsync(Guid.NewGuid())).Items);
        Assert.Equal(new("next draft", 2), await drafts.GetAsync(scope, fixture.Token));
    }
}
