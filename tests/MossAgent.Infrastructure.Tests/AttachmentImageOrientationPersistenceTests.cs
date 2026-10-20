using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using SkiaSharp;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AttachmentImageOrientationPersistenceTests
{
    public static TheoryData<SKEncodedOrigin, bool> Cases
    {
        get
        {
            var cases = new TheoryData<SKEncodedOrigin, bool>();
            foreach (var origin in Enumerable.Range(1, 8))
                foreach (var projectless in new[] { false, true })
                    cases.Add((SKEncodedOrigin)origin, projectless);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OriginalExifBytesSurviveSelectionDraftAndHistoryWhileBothPreviewsAreOriented(
        SKEncodedOrigin origin, bool projectless)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var task = projectless ? fixture.Task with { Id = Guid.NewGuid(), ProjectId = null } : fixture.Task;
        await fixture.Repository.SaveTaskAsync(task, fixture.Token);
        var bytes = AttachmentOrientationTestData.Jpeg(origin, large: true, littleEndian: true);
        var source = Path.Combine(fixture.Project.PrimaryDirectory, "oriented.jpg");
        await File.WriteAllBytesAsync(source, bytes, fixture.Token);
        var decoder = new SkiaAttachmentImageDecoder();
        var reader = new WorkspaceAttachmentReader(decoder);
        var selected = Assert.Single(projectless ? await reader.ReadSelectedAsync([source], fixture.Token)
            : await reader.ReadAsync(fixture.Project, [source], fixture.Token));
        File.Delete(source);
        var paths = new AppDataPaths(fixture.Project.PrimaryDirectory);
        var scope = new ConversationDraftScope(projectless ? null : fixture.Project.Id, projectless ? null : fixture.Task.Id);
        var stamp = projectless ? WorkspaceAttachmentAuthorization.CreateSelection(source)
            : WorkspaceAttachmentAuthorization.Create(fixture.Project);
        var item = new ConversationDraftAttachment(Guid.NewGuid(), source, "oriented.jpg", null, selected.ByteCount,
            stamp, selected.MediaType, selected.Image);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections, paths, images: decoder);
        Assert.True(await drafts.SaveAsync([new(scope, "keep this draft", 0, [item])], fixture.Token));
        var reopened = await new SqliteConversationDraftRepository(fixture.Connections, paths, images: decoder)
            .GetAsync(scope, fixture.Token);
        var restored = Assert.Single(reopened!.AttachmentItems);
        Assert.True(restored.IsAvailable);
        Assert.Equal(Convert.ToBase64String(bytes), restored.Image!.Base64Data);
        Assert.Equal(300, restored.Image.Width);
        Assert.Equal(200, restored.Image.Height);
        var artifacts = new TaskArtifactPaths(paths);
        var store = new ConversationAttachmentStore(artifacts, images: decoder);
        var message = new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, "sent body", DateTimeOffset.UtcNow);
        var metadata = await store.SaveAsync(message.TaskId, message.Id,
            [new(source, restored.DisplayPath, "", restored.ByteCount, restored.Image)], fixture.Token);
        await fixture.Repository.AppendMessageAsync(message with { Attachments = metadata }, fixture.Token);
        var stored = Assert.Single(await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token));
        var historical = Assert.Single(await new ConversationAttachmentStore(artifacts, images: decoder)
            .ReadAsync(stored.TaskId, stored.Id, stored.Attachments!, fixture.Token));
        Assert.Equal(Convert.ToBase64String(bytes), historical.Image!.Base64Data);
        Assert.Equal("image/jpeg", Assert.Single(stored.Attachments!).MediaType);
        var renderer = new SkiaAttachmentThumbnailRenderer();
        var draftPreview = await renderer.RenderAsync(restored.Image, fixture.Token);
        var historyPreview = await renderer.RenderAsync(historical.Image, fixture.Token);
        Assert.Equal((int)origin >= 5 ? 64 : 96, historyPreview.Width);
        Assert.Equal((int)origin >= 5 ? 96 : 64, historyPreview.Height);
        Assert.Equal(draftPreview.Base64Data, historyPreview.Base64Data);
        Assert.False(File.Exists(source));
        Assert.Equal("keep this draft", reopened.Text);
        Assert.Equal("sent body", stored.Content);
        var storedTask = Assert.Single(await fixture.Repository.GetTasksAsync(task.ProjectId, fixture.Token));
        Assert.Equal(task.Id, storedTask.Id);
        Assert.Equal(projectless, storedTask.ProjectId is null);
    }
}
