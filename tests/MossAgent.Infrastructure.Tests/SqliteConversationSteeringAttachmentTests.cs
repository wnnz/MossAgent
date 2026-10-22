using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringAttachmentTests
{
    [Fact]
    public async Task AcceptedSnapshotConsumesTheDraftAttachmentAndBecomesAMessageAttachment()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var drafts = new SqliteConversationDraftRepository(fixture.Connections, new AppDataPaths(fixture.Project.PrimaryDirectory));
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        var draftAttachmentId = Guid.NewGuid();
        var stamp = WorkspaceAttachmentAuthorization.Create(fixture.Project);
        var draftItem = new ConversationDraftAttachment(draftAttachmentId, Path.Combine(fixture.Project.PrimaryDirectory, "guide.txt"),
            "guide.txt", "body", 4, stamp, "text/plain", null);
        Assert.True(await drafts.SaveAsync([new(scope, "guide this run", 0, [draftItem])], fixture.Token));
        var saved = Assert.IsType<ConversationDraft>(await drafts.GetAsync(scope, fixture.Token));
        var run = await fixture.OpenAsync();

        var inputId = Guid.NewGuid();
        var snapshot = new ConversationAttachment(Guid.NewGuid(), fixture.Task.Id, inputId, "guide.txt", 4,
            new string('A', 64), "text/plain");
        var receipt = Assert.IsType<ConversationSteeringAcceptance>(await fixture.Repository.AcceptAsync(run.TaskId, run.Id,
            inputId, "guide this run", new(scope, string.Empty, saved.Revision, []), fixture.Token, null,
            [snapshot], [draftAttachmentId]));

        Assert.False(receipt.AlreadyAccepted);
        Assert.Equal(snapshot, Assert.Single(receipt.Input.AttachmentItems));
        var cleared = Assert.IsType<ConversationDraft>(await drafts.GetAsync(scope, fixture.Token));
        Assert.Empty(cleared.Text);
        Assert.Empty(cleared.AttachmentItems);
        Assert.Equal(snapshot, Assert.Single(Assert.Single((await fixture.InputsAsync()).Items).AttachmentItems));

        var applied = Assert.IsType<ConversationSteeringApplication>(await fixture.Repository.ApplyAsync(
            await fixture.ReloadAsync(run), [inputId], null, fixture.Token));
        Assert.Equal(snapshot, Assert.Single(Assert.Single(applied.Messages).Attachments!));
        var stored = Assert.Single(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(inputId, stored.Id);
        Assert.Equal(snapshot, Assert.Single(stored.Attachments!));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM conversation_attachments WHERE message_id='" + inputId + "';"));
    }
}
