using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;
using static MossAgent.Infrastructure.Tests.ConversationDraftAttachmentTestData;

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

    [Theory]
    [InlineData("conflict")]
    [InlineData("attachment-write-failure")]
    public async Task FailedAttachmentAcceptanceRollsBackTheReceiptAndPreservesTheDraft(string kind)
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var repository = new SqliteConversationSteeringRepository(fixture.Connections,
            paths: new AppDataPaths(fixture.Project.PrimaryDirectory));
        var drafts = Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var kept = Attachment(fixture.Inner, "kept draft bytes");
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0, [kept])], fixture.Token));
        var run = await fixture.OpenAsync();
        if (kind == "attachment-write-failure")
            await fixture.ExecuteAsync("CREATE TRIGGER reject_steering_attachment BEFORE INSERT ON conversation_steering_input_attachments BEGIN SELECT RAISE(ABORT,'synthetic attachment failure'); END;");
        var rolledBack = Attachment(fixture.Inner, "rolled-back snapshot bytes");
        var inputId = Guid.NewGuid();
        var operation = () => repository.AcceptAsync(run.TaskId, run.Id, inputId, "guidance",
            new ConversationDraftUpdate(scope, string.Empty, kind == "conflict" ? 0 : 1, [rolledBack]), fixture.Token);
        if (kind == "conflict") await Assert.ThrowsAsync<ConversationDraftConflictException>(operation);
        else await Assert.ThrowsAsync<SqliteException>(operation);
        Assert.Empty((await fixture.InputsAsync()).Items);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_input_attachments;"));
        Assert.Equal(run, await fixture.ReloadAsync(run));
        var draft = Assert.IsType<ConversationDraft>(await drafts.GetAsync(scope, fixture.Token));
        Assert.Equal("guidance", draft.Text);
        Assert.Equal(1, draft.Revision);
        Assert.Equal(kept, Assert.Single(draft.AttachmentItems));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
        Assert.True(File.Exists(Snapshot(fixture.Inner, kept)));
        Assert.True(File.Exists(Snapshot(fixture.Inner, rolledBack)));
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

    [Fact]
    public async Task AttachmentSnapshotsTransferIntoTheInputAndTheDraftIsConsumedAtomically()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var repository = new SqliteConversationSteeringRepository(fixture.Connections,
            paths: new AppDataPaths(fixture.Project.PrimaryDirectory));
        var drafts = Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = Attachment(fixture.Inner, "steered file text");
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0, [attachment])], fixture.Token));
        var run = await fixture.OpenAsync();
        var id = Guid.NewGuid();
        var ack = Assert.IsType<ConversationSteeringAcceptance>(await repository.AcceptAsync(run.TaskId, run.Id, id,
            "guidance", new ConversationDraftUpdate(scope, string.Empty, 1, [attachment]), fixture.Token));
        Assert.False(ack.AlreadyAccepted);
        var persisted = Assert.Single(ack.Input.AttachmentItems);
        Assert.Equal(attachment.Id, persisted.Id);
        Assert.Equal(attachment.DisplayPath, persisted.DisplayPath);
        Assert.Equal(attachment.ByteCount, persisted.ByteCount);
        Assert.Equal(64, persisted.Sha256.Length);
        Assert.Equal("text/plain", persisted.MediaType);
        Assert.Equal(id, persisted.MessageId);
        Assert.Equal(run.TaskId, persisted.TaskId);
        var draft = Assert.IsType<ConversationDraft>(await drafts.GetAsync(scope, fixture.Token));
        Assert.Equal(string.Empty, draft.Text);
        Assert.Empty(draft.AttachmentItems);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_input_attachments;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
        Assert.True(File.Exists(Snapshot(fixture.Inner, attachment)));
        var pending = Assert.IsType<ConversationSteeringPending>(await repository.GetPendingAsync(run.TaskId, run.Id, fixture.Token));
        Assert.Equal(attachment.Id, Assert.Single(Assert.Single(pending.Inputs).AttachmentItems).Id);
        var page = await repository.GetInputsAsync(run.TaskId, long.MaxValue, 64, fixture.Token);
        Assert.Equal(attachment.Id, Assert.Single(Assert.Single(page.Items).AttachmentItems).Id);
    }

    [Fact]
    public async Task AcceptedAttachmentsParticipateInReceiptIdentityIdempotency()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var repository = new SqliteConversationSteeringRepository(fixture.Connections,
            paths: new AppDataPaths(fixture.Project.PrimaryDirectory));
        var drafts = Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = Attachment(fixture.Inner);
        var other = Attachment(fixture.Inner, "different attachment");
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0, [attachment])], fixture.Token));
        var run = await fixture.OpenAsync();
        var id = Guid.NewGuid();
        var ack = Assert.IsType<ConversationSteeringAcceptance>(await repository.AcceptAsync(run.TaskId, run.Id, id,
            "guidance", new ConversationDraftUpdate(scope, string.Empty, 1, [attachment]), fixture.Token));
        var duplicate = Assert.IsType<ConversationSteeringAcceptance>(await repository.AcceptAsync(run.TaskId, run.Id, id,
            "guidance", new ConversationDraftUpdate(scope, string.Empty, 1, [attachment]), fixture.Token));
        Assert.True(duplicate.AlreadyAccepted);
        Assert.Equal(ack.Input.Id, duplicate.Input.Id);
        Assert.Equal(ack.Input.AttachmentItems, duplicate.Input.AttachmentItems);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_input_attachments;"));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.AcceptAsync(run.TaskId, run.Id, id,
            "guidance", new ConversationDraftUpdate(scope, string.Empty, 1, [other]), fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.AcceptAsync(run.TaskId, run.Id, id,
            "guidance", new ConversationDraftUpdate(scope, string.Empty, 1), fixture.Token));
    }

    [Fact]
    public async Task AttachmentsWithoutSnapshotStorageFailBeforePersistingAnything()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var drafts = Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = Attachment(fixture.Inner);
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0, [attachment])], fixture.Token));
        var run = await fixture.OpenAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.AcceptAsync(run.TaskId, run.Id,
            Guid.NewGuid(), "guidance", new ConversationDraftUpdate(scope, string.Empty, 1, [attachment]), fixture.Token));
        Assert.Empty((await fixture.InputsAsync()).Items);
        var draft = Assert.IsType<ConversationDraft>(await drafts.GetAsync(scope, fixture.Token));
        Assert.Equal("guidance", draft.Text);
        Assert.Equal(attachment, Assert.Single(draft.AttachmentItems));
    }
}
