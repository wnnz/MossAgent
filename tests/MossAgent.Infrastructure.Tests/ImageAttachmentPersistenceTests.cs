using System.Security.Cryptography;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ImageAttachmentPersistenceTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    public async Task SelectedImagesReopenFromBinarySnapshotsAndMetadataAfterOriginalDeletion(string mediaType)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var bytes = AttachmentImageTestData.Static(mediaType);
        var source = Path.Combine(fixture.Project.PrimaryDirectory, "selected.data");
        await File.WriteAllBytesAsync(source, bytes, fixture.Token);
        var selected = Assert.Single(await new WorkspaceAttachmentReader(new SkiaAttachmentImageDecoder())
            .ReadAsync(fixture.Project, [source], fixture.Token));
        Assert.Equal(mediaType, selected.MediaType);
        Assert.Equal(string.Empty, selected.Content);
        File.Delete(source);
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var store = new ConversationAttachmentStore(paths, images: new SkiaAttachmentImageDecoder());
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "", DateTimeOffset.UtcNow);
        var metadata = await store.SaveAsync(fixture.Task.Id, message.Id, [selected], fixture.Token);
        await fixture.Repository.AppendMessageAsync(message with { Attachments = metadata }, fixture.Token);
        var stored = Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        var restored = Assert.Single(await new ConversationAttachmentStore(paths, images: new SkiaAttachmentImageDecoder())
            .ReadAsync(fixture.Task.Id, message.Id, stored.Attachments!, fixture.Token));
        Assert.Equal(Convert.ToBase64String(bytes), restored.Image?.Base64Data);
        Assert.Equal(mediaType, restored.MediaType);
        Assert.Equal(string.Empty, restored.Content);
        var snapshot = Assert.Single(stored.Attachments!);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), snapshot.Sha256);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Snapshot(paths, snapshot), fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_table_info('conversation_attachments') WHERE name IN ('content','image','base64_data');"));
        Assert.DoesNotContain(Directory.GetFiles(fixture.Project.PrimaryDirectory, "*", SearchOption.AllDirectories), path => path.EndsWith(".tmp"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("length")]
    [InlineData("hash")]
    [InlineData("mime")]
    [InlineData("codec")]
    public async Task DamagedOrRetypedImageSnapshotsNeverReturnContent(string damage)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var bytes = AttachmentImageTestData.Static("image/png");
        var image = await new SkiaAttachmentImageDecoder().DecodeAsync(bytes, fixture.Token);
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var store = new ConversationAttachmentStore(paths, images: new SkiaAttachmentImageDecoder());
        var messageId = Guid.NewGuid();
        var metadata = Assert.Single(await store.SaveAsync(fixture.Task.Id, messageId,
            [new("source.png", "selected.png", "", image.ByteCount, image)], fixture.Token));
        var path = Snapshot(paths, metadata);
        if (damage == "missing") File.Delete(path);
        else if (damage == "mime") metadata = metadata with { MediaType = "image/jpeg" };
        else
        {
            var changed = damage == "length" ? bytes[..^1] : new byte[bytes.Length];
            await File.WriteAllBytesAsync(path, changed, fixture.Token);
            if (damage == "codec") metadata = metadata with { Sha256 = Convert.ToHexString(SHA256.HashData(changed)) };
        }
        var failure = await Record.ExceptionAsync(() => store.ReadAsync(fixture.Task.Id, messageId, [metadata], fixture.Token));
        Assert.True(failure is IOException or InvalidDataException);
    }

    [Fact]
    public async Task ForgedEnvelopesAreDecodedBeforeAnySnapshotIsPublished()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var bytes = AttachmentImageTestData.Static("image/png")[..12];
        var forged = AttachmentImage.FromDecodedBytes("image/png", bytes, 1, 1);
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var store = new ConversationAttachmentStore(paths, images: new SkiaAttachmentImageDecoder());
        await Assert.ThrowsAsync<AttachmentImageValidationException>(() => store.SaveAsync(fixture.Task.Id, Guid.NewGuid(),
            [new("never-read.png", "forged.png", "", bytes.Length, forged)], fixture.Token));
        Assert.Empty(Directory.GetFiles(fixture.Project.PrimaryDirectory, "*.txt", SearchOption.AllDirectories));
    }

    private static string Snapshot(TaskArtifactPaths paths, ConversationAttachment item) =>
        Path.Combine(paths.GetTaskDirectory(item.TaskId), "attachments", item.MessageId.ToString("N"), item.Id.ToString("N") + ".txt");
}
