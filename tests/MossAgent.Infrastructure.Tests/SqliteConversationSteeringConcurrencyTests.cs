using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;
using static MossAgent.Infrastructure.Tests.ConversationDraftAttachmentTestData;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringConcurrencyTests
{
    [Fact]
    public async Task SteeringAcceptanceAndFollowupEnqueueConsumeTheSameDraftScopeOnce()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var repository = new SqliteConversationSteeringRepository(fixture.Connections,
            paths: new AppDataPaths(fixture.Project.PrimaryDirectory));
        var drafts = Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = Attachment(fixture.Inner, "contended snapshot bytes");
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0, [attachment])], fixture.Token));
        var run = await fixture.OpenAsync();
        var followups = new SqliteConversationFollowupRepository(fixture.Connections);
        var item = ConversationFollowupTestData.Create(fixture.Task, "queued instead");
        var accept = repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "guidance",
            new ConversationDraftUpdate(scope, string.Empty, 1, [attachment]), fixture.Token);
        var enqueue = followups.SaveAsync(new(fixture.Task.Id, 0, [item]),
            [new ConversationDraftUpdate(scope, "queued draft", 1)], fixture.Token);
        ConversationSteeringAcceptance? ack = null;
        try { ack = Assert.IsType<ConversationSteeringAcceptance>(await accept); }
        catch (ConversationDraftConflictException) { }
        var queued = await enqueue;
        Assert.NotEqual(ack is not null, queued);
        var draft = Assert.IsType<ConversationDraft>(await drafts.GetAsync(scope, fixture.Token));
        Assert.Equal(2, draft.Revision);
        Assert.Empty(draft.AttachmentItems);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
        if (ack is not null)
        {
            Assert.False(queued);
            Assert.Equal(string.Empty, draft.Text);
            Assert.Equal(attachment.Id, Assert.Single(ack.Input.AttachmentItems).Id);
            Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_inputs;"));
            Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_input_attachments;"));
            Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_followups;"));
            Assert.Empty((await followups.GetAsync(fixture.Task.Id, fixture.Token)).Items);
        }
        else
        {
            Assert.True(queued);
            Assert.Equal("queued draft", draft.Text);
            Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_inputs;"));
            Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_input_attachments;"));
            Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_followups;"));
            Assert.Equal(item.Id, Assert.Single((await followups.GetAsync(fixture.Task.Id, fixture.Token)).Items).Id);
        }
    }

    [Fact]
    public async Task CompetingRegistrationAndExactRetriesCreateOneRunAndOneReceipt()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var first = fixture.Repository;
        var other = new SqliteConversationSteeringRepository(fixture.Connections);
        var attempts = await Task.WhenAll(first.OpenAsync(fixture.Task, Guid.NewGuid(), fixture.Token),
            other.OpenAsync(fixture.Task, Guid.NewGuid(), fixture.Token));
        var run = Assert.IsType<ConversationSteeringRun>(Assert.Single(attempts, result => result is not null));
        var id = Guid.NewGuid();
        var receipts = await Task.WhenAll(first.AcceptAsync(run.TaskId, run.Id, id, "once", null, fixture.Token),
            other.AcceptAsync(run.TaskId, run.Id, id, "once", null, fixture.Token));
        Assert.All(receipts, Assert.NotNull);
        Assert.Single(receipts, receipt => !receipt!.AlreadyAccepted);
        Assert.Single(receipts, receipt => receipt!.AlreadyAccepted);
        Assert.Equal(receipts[0]!.Input, receipts[1]!.Input);
        Assert.Equal(1, (await fixture.ReloadAsync(run)).Revision);
        Assert.Single((await fixture.InputsAsync()).Items);
    }

    [Fact]
    public async Task AcceptanceAndCompletionRaceCannotLoseAnAcknowledgedInput()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        for (var index = 0; index < 12; index++)
        {
            var run = await fixture.OpenAsync();
            var accept = fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "racing", null, fixture.Token);
            var close = fixture.Repository.CloseAsync(run, false, fixture.Token);
            await Task.WhenAll(accept, close);
            var accepted = await accept;
            var closed = await close;
            var saved = await fixture.ReloadAsync(run);
            if (accepted is null)
            {
                Assert.True(closed);
                Assert.False(saved.IsOpen);
            }
            else
            {
                Assert.False(closed);
                Assert.True(saved.IsOpen);
                Assert.Equal(ConversationSteeringInputState.Pending, accepted.Input.State);
                Assert.True(await fixture.Repository.CloseAsync(saved, true, fixture.Token));
            }
        }
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token));
    }

    [Fact]
    public async Task CompetingAppliesCommitOnceAndClosingAnotherTaskDoesNotInterfere()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var input = await fixture.AcceptAsync(run, "lane A");
        run = await fixture.ReloadAsync(run);
        var taskB = fixture.Task with { Id = Guid.NewGuid() };
        await fixture.Workspaces.SaveTaskAsync(taskB, fixture.Token);
        var runB = await fixture.OpenAsync(taskB);
        var inputB = await fixture.AcceptAsync(runB, "lane B");
        var other = new SqliteConversationSteeringRepository(fixture.Connections);
        var applies = await Task.WhenAll(fixture.Repository.ApplyAsync(run, [input.Id], null, fixture.Token),
            other.ApplyAsync(run, [input.Id], null, fixture.Token));
        Assert.Single(applies, result => result is not null);
        Assert.True(await fixture.Repository.CloseAsync(await fixture.ReloadAsync(run), false, fixture.Token));
        Assert.True((await fixture.ReloadAsync(runB)).IsOpen);
        Assert.NotNull(await other.ApplyAsync(await fixture.ReloadAsync(runB), [inputB.Id], null, fixture.Token));
        Assert.Equal("lane A", Assert.Single(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token)).Content);
        Assert.Equal("lane B", Assert.Single(await fixture.Workspaces.GetMessagesAsync(runB.TaskId, fixture.Token)).Content);
    }
}
