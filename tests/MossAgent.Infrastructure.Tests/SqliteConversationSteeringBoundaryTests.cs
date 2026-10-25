using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;
using static MossAgent.Infrastructure.Tests.ConversationDraftAttachmentTestData;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringBoundaryTests
{
    [Fact]
    public async Task OnlyTheCurrentPendingPrefixIsAppliedAndCannotBeReplayed()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var first = await fixture.AcceptAsync(run, "first");
        var second = await fixture.AcceptAsync(run, "second");
        var tool = new ConversationMessage(Guid.NewGuid(), run.TaskId, MessageRole.Tool, "completed tool", DateTimeOffset.UtcNow.AddMinutes(1), "call-1");
        await fixture.Workspaces.AppendMessageAsync(tool, fixture.Token);
        Assert.Null(await fixture.Repository.ApplyAsync(run, [first.Id], null, fixture.Token));
        run = await fixture.ReloadAsync(run);
        Assert.Null(await fixture.Repository.ApplyAsync(run, [second.Id], null, fixture.Token));
        var assistant = new ConversationMessage(Guid.NewGuid(), run.TaskId, MessageRole.Assistant, "streamed once", DateTimeOffset.UtcNow);
        Assert.NotNull(await fixture.Repository.ApplyAsync(run, [first.Id, second.Id], assistant, fixture.Token));
        var messages = await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token);
        Assert.Equal(new[] { tool.Id, assistant.Id, first.Id, second.Id }, messages.Select(message => message.Id));
        Assert.True(messages[1].CreatedAt > tool.CreatedAt);
        Assert.Null(await fixture.Repository.ApplyAsync(run, [first.Id, second.Id], assistant, fixture.Token));
        Assert.Equal(4, (await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token)).Count);
        Assert.All((await fixture.InputsAsync()).Items, input =>
        {
            Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary, input.State);
            Assert.NotNull(input.AppliedAt);
            Assert.Null(input.InterruptedAt);
        });
        Assert.Equal(3, (await fixture.ReloadAsync(run)).Revision);
    }

    [Fact]
    public async Task MessageFailureRollsBackAssistantInputsAndReceiptTransitions()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var first = await fixture.AcceptAsync(run, "first");
        var second = await fixture.AcceptAsync(run, "rejected input");
        run = await fixture.ReloadAsync(run);
        await fixture.ExecuteAsync("CREATE TRIGGER reject_boundary BEFORE INSERT ON messages WHEN NEW.content='rejected input' BEGIN SELECT RAISE(ABORT,'synthetic boundary failure'); END;");
        var assistant = new ConversationMessage(Guid.NewGuid(), run.TaskId, MessageRole.Assistant, "retain once", DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.ApplyAsync(run, [first.Id, second.Id], assistant, fixture.Token));
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
        Assert.Equal(run, await fixture.ReloadAsync(run));
        Assert.All((await fixture.InputsAsync()).Items, input => Assert.Equal(ConversationSteeringInputState.Pending, input.State));
        await fixture.ExecuteAsync("DROP TRIGGER reject_boundary;");
        Assert.NotNull(await fixture.Repository.ApplyAsync(run, [first.Id, second.Id], assistant, fixture.Token));
        Assert.Equal(3, (await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token)).Count);
    }

    [Fact]
    public async Task NormalSealingRefusesPendingAndInterruptionNeverCreatesMessages()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        await fixture.AcceptAsync(run);
        Assert.False(await fixture.Repository.CloseAsync(run, true, fixture.Token));
        run = await fixture.ReloadAsync(run);
        Assert.False(await fixture.Repository.CloseAsync(run, false, fixture.Token));
        Assert.True(await fixture.Repository.CloseAsync(run, true, fixture.Token));
        Assert.False((await fixture.ReloadAsync(run)).IsOpen);
        Assert.False(await fixture.Repository.CloseAsync(run, true, fixture.Token));
        Assert.Equal(ConversationSteeringInputState.Interrupted, Assert.Single((await fixture.InputsAsync()).Items).State);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
        Assert.Null(await fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "late", null, fixture.Token));
        Assert.Null(await fixture.Repository.OpenAsync(fixture.Task, run.Id, fixture.Token));
        var fresh = await fixture.OpenAsync();
        Assert.NotEqual(run.Id, fresh.Id);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
    }

    [Fact]
    public async Task ApplicationMaterializesTheMessageAndAttachmentRowsAtomically()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var paths = new AppDataPaths(fixture.Project.PrimaryDirectory);
        var repository = new SqliteConversationSteeringRepository(fixture.Connections, paths: paths);
        var drafts = Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = Attachment(fixture.Inner, "materialized attachment text");
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0, [attachment])], fixture.Token));
        var run = await fixture.OpenAsync();
        var inputId = Guid.NewGuid();
        Assert.NotNull(await repository.AcceptAsync(run.TaskId, run.Id, inputId, "guidance",
            new ConversationDraftUpdate(scope, string.Empty, 1, [attachment]), fixture.Token));
        run = await fixture.ReloadAsync(run);
        var application = Assert.IsType<ConversationSteeringApplication>(
            await repository.ApplyAsync(run, [inputId], null, fixture.Token));
        var message = Assert.Single(application.Messages);
        Assert.Equal(inputId, message.Id);
        Assert.Equal(MessageRole.User, message.Role);
        var materialized = Assert.Single(message.Attachments!);
        Assert.Equal(attachment.Id, materialized.Id);
        Assert.Equal(inputId, materialized.MessageId);
        Assert.Equal(attachment.DisplayPath, materialized.DisplayPath);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_attachments;"));
        var stored = Assert.Single(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
        Assert.Equal(inputId, stored.Id);
        Assert.Equal(attachment.Id, Assert.Single(stored.Attachments!).Id);
        var store = new ConversationAttachmentStore(new TaskArtifactPaths(paths), appPaths: paths);
        var contents = await store.ReadAsync(run.TaskId, inputId, stored.Attachments!, fixture.Token);
        Assert.Equal("materialized attachment text", Assert.Single(contents).Content);
    }
}
