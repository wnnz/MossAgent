using MossAgent.Domain;
using MossAgent.Application.Attachments;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationFollowupAttachmentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileBackedSnapshotsRemainBoundToTheSameMessageAcrossQueuedDispatch(bool image)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var decoder = new SkiaAttachmentImageDecoder();
        var files = new ConversationAttachmentStore(paths, images: decoder);
        var item = ConversationFollowupTestData.Create(fixture.Task);
        var pixels = image ? await decoder.DecodeAsync(AttachmentImageTestData.Static("image/png"), fixture.Token) : null;
        var selected = image ? new WorkspaceAttachmentContent("selected.png", "selected.png", "", pixels!.ByteCount, pixels)
            : new WorkspaceAttachmentContent("note.txt", "note.txt", "outside snapshot", 16);
        var metadata = await files.SaveAsync(fixture.Task.Id, item.Id, [selected], fixture.Token);
        item = item with { Attachments = metadata, Model = item.Model with { SupportsImages = image } };
        Assert.True(await repository.SaveAsync(new(item.TaskId, 0, [item]), [], fixture.Token));
        var saved = Assert.Single((await new SqliteConversationFollowupRepository(fixture.Connections).GetAsync(item.TaskId, fixture.Token)).Items);
        Assert.Equal(metadata, saved.Attachments);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_followups WHERE instr(payload,'Base64Data')>0 OR instr(payload,'outside snapshot')>0;"));
        var before = Assert.Single(await files.ReadAsync(item.TaskId, item.Id, saved.Attachments, fixture.Token));
        Assert.Equal(selected.Content, before.Content);
        Assert.Equal(selected.Image?.Base64Data, before.Image?.Base64Data);
        var prepared = await ConversationFollowupDispatchTestData.PrepareAsync(fixture, fixture.Task, item);
        var dispatch = Assert.IsType<ConversationFollowupDispatch>(await repository.DispatchAsync(prepared, fixture.Token));
        var message = Assert.Single(await fixture.Repository.GetMessagesAsync(item.TaskId, fixture.Token));
        Assert.Equal(dispatch.Message.Id, message.Id);
        Assert.Equal(metadata, message.Attachments!);
        var restored = Assert.Single(await new ConversationAttachmentStore(paths, images: decoder)
            .ReadAsync(item.TaskId, message.Id, message.Attachments!, fixture.Token));
        Assert.Equal(selected.Content, restored.Content);
        Assert.Equal(selected.Image?.Base64Data, restored.Image?.Base64Data);
    }

    [Fact]
    public async Task ForeignDraftUpdatesAndUnrelatedDraftStoresCannotBeBound()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await using var other = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationFollowupRepository(fixture.Connections);
        Assert.True(repository.SupportsDrafts(new SqliteConversationDraftRepository(fixture.Connections)));
        Assert.False(repository.SupportsDrafts(new SqliteConversationDraftRepository(other.Connections)));
        var item = ConversationFollowupTestData.Create(fixture.Task);
        ConversationDraftUpdate[] updates = [new(new(fixture.Task.ProjectId, Guid.NewGuid()), "", 0)];
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(new(item.TaskId, 0, [item]), updates, fixture.Token));
        Assert.Empty((await repository.GetAsync(item.TaskId, fixture.Token)).Items);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_drafts;"));
    }
}
