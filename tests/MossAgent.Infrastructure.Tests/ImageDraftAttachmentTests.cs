using System.Security.Cryptography;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ImageDraftAttachmentTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    public async Task DraftImagesReopenWithoutOriginalFilesAndConflictsCannotReplaceTheirBytes(string mediaType)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var original = await AttachmentAsync(fixture, mediaType);
        var repository = Repository(fixture);
        Assert.True(await repository.SaveAsync([new(scope, "draft body", 0, [original])], fixture.Token));
        Assert.False(await repository.SaveAsync([new(scope, "conflict", 0)], fixture.Token));
        var restored = (await Repository(fixture).GetAsync(scope, fixture.Token))!;
        var item = Assert.Single(restored.AttachmentItems);
        Assert.Equal("draft body", restored.Text);
        Assert.Equal(1, restored.Revision);
        Assert.Equal(original.Id, item.Id);
        Assert.Equal(mediaType, item.MediaType);
        Assert.Null(item.Content);
        Assert.True(item.IsAvailable);
        Assert.Equal(original.Image?.Base64Data, item.Image?.Base64Data);
        Assert.False(File.Exists(original.FullPath));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_table_info('conversation_draft_attachments') WHERE name IN ('content','image','base64_data');"));
        var replacement = await AttachmentAsync(fixture, mediaType == "image/jpeg" ? "image/png" : "image/jpeg");
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(
            [new(scope, "must not overwrite", 1, [replacement with { Id = original.Id }])], fixture.Token));
        Assert.Equal(original.Image?.Base64Data, Assert.Single((await repository.GetAsync(scope, fixture.Token))!.AttachmentItems).Image?.Base64Data);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("length")]
    [InlineData("hash")]
    [InlineData("mime")]
    [InlineData("codec")]
    public async Task UnavailableImagesRetainTypedRemovableMetadataAndNeverBecomeEmptyText(string damage)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var original = await AttachmentAsync(fixture, "image/png");
        var repository = Repository(fixture);
        Assert.True(await repository.SaveAsync([new(scope, "preserved body", 0, [original])], fixture.Token));
        var path = ConversationDraftAttachmentTestData.Snapshot(fixture, original);
        if (damage == "missing") File.Delete(path);
        else if (damage == "mime") await fixture.ExecuteAsync("UPDATE conversation_draft_attachments SET media_type='image/jpeg';");
        else
        {
            var changed = new byte[original.ByteCount - (damage == "length" ? 1 : 0)];
            await File.WriteAllBytesAsync(path, changed, fixture.Token);
            if (damage == "codec") await fixture.ExecuteAsync($"UPDATE conversation_draft_attachments SET sha256='{Convert.ToHexString(SHA256.HashData(changed))}';");
        }
        var restored = (await Repository(fixture).GetAsync(scope, fixture.Token))!;
        var item = Assert.Single(restored.AttachmentItems);
        Assert.False(item.IsAvailable);
        Assert.Null(item.Content);
        Assert.Null(item.Image);
        Assert.Equal(damage == "mime" ? "image/jpeg" : "image/png", item.MediaType);
        Assert.Equal(original.Id, item.Id);
        Assert.Equal("preserved body", restored.Text);
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync([new(scope, "edit", 1, [item])], fixture.Token));
        Assert.True(await repository.SaveAsync([new(scope, "edited body", 1)], fixture.Token));
        Assert.Empty((await repository.GetAsync(scope, fixture.Token))!.AttachmentItems);
        Assert.Equal("edited body", (await repository.GetAsync(scope, fixture.Token))!.Text);
    }

    private static SqliteConversationDraftRepository Repository(SqliteFailurePersistenceFixture fixture) =>
        new(fixture.Connections, new AppDataPaths(fixture.Project.PrimaryDirectory), images: new SkiaAttachmentImageDecoder());

    private static async Task<ConversationDraftAttachment> AttachmentAsync(SqliteFailurePersistenceFixture fixture, string mediaType)
    {
        var image = await new SkiaAttachmentImageDecoder().DecodeAsync(AttachmentImageTestData.Static(mediaType), fixture.Token);
        return new(Guid.NewGuid(), Path.Combine(fixture.Project.PrimaryDirectory, "deleted-source.data"), "selected.data", null,
            image.ByteCount, WorkspaceAttachmentAuthorization.Create(fixture.Project), image.MediaType, image);
    }
}
