using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AttachmentThumbnailWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoredDraftImagesUseSnapshotBytesWithoutReadingTheOriginalPath(bool projectless)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.ThumbnailTests", Guid.NewGuid().ToString("N"));
        var project = projectless ? null : new ProjectProfile(Guid.NewGuid(), "thumbnail", root, [root], DateTimeOffset.UtcNow);
        var scope = new ConversationDraftScope(project?.Id, null);
        var path = Path.Combine(root, "deleted-original.png");
        var image = ThumbnailTestRenderer.Image;
        var stamp = project is null ? WorkspaceAttachmentAuthorization.CreateSelection(path) : WorkspaceAttachmentAuthorization.Create(project);
        var attachment = new ConversationDraftAttachment(Guid.NewGuid(), path, "deleted-original.png", null,
            image.ByteCount, stamp, image.MediaType, image);
        var repository = new TestConversationDraftRepository();
        await repository.SaveAsync([new(scope, "draft text", 0, [attachment])], Token);
        using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.Zero);
        var probe = new WorkspaceAttachmentOperationProbe();
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe,
            thumbnailRenderer: new SkiaAttachmentThumbnailRenderer());
        using var coordinator = new WorkspaceAttachmentDraftCoordinator(manager, drafts);
        coordinator.SelectContext(project, null);
        await drafts.LoadCurrentAsync();

        var restored = Assert.Single(manager.Items);
        var thumbnail = await Assert.IsType<AttachmentThumbnailSource>(restored.Thumbnail).LoadAsync(Token);

        Assert.False(File.Exists(path));
        Assert.Equal(attachment.Id, restored.SelectionId);
        Assert.Equal("draft text", drafts.Text);
        Assert.Equal("image/png", thumbnail.MediaType);
        Assert.Equal(1, thumbnail.Width);
        Assert.Equal(image.Base64Data, restored.Content.Image!.Base64Data);
        Assert.True(drafts.CanSend);
        restored.RemoveCommand.Execute(null);
        Assert.Empty(manager.Items);
        Assert.Empty(drafts.Attachments);
    }

    [Fact]
    public void TextAndUnavailableAttachmentsNeverCreateThumbnailRequests()
    {
        var renderer = new ThumbnailTestRenderer();
        var probe = new WorkspaceAttachmentOperationProbe();
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe, thumbnailRenderer: renderer);
        var image = ThumbnailTestRenderer.Image;
        var text = manager.CreateItem(new("missing.txt", "missing.txt", "text", 4));
        var unavailable = manager.CreateItem(new("missing.png", "missing.png", "", image.ByteCount, image), isAvailable: false);

        Assert.Null(text.Thumbnail);
        Assert.Null(unavailable.Thumbnail);
        Assert.Empty(renderer.Requests);
    }

    [Fact]
    public async Task ProductionCompositionInjectsTheRendererIntoSelectedAttachmentRows()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.ThumbnailComposition", Guid.NewGuid().ToString("N"));
        await using var services = AppComposition.CreateServiceCollection(root).BuildServiceProvider(validateScopes: true);
        var manager = services.GetRequiredService<WorkspaceAttachmentManagerViewModel>();
        Assert.IsType<SkiaAttachmentThumbnailRenderer>(services.GetRequiredService<IAttachmentThumbnailRenderer>());
        var image = ThumbnailTestRenderer.Image;
        var row = manager.CreateItem(new("never-read.png", "never-read.png", "", image.ByteCount, image));

        var result = await Assert.IsType<AttachmentThumbnailSource>(row.Thumbnail).LoadAsync(Token);

        Assert.Equal(1, result.Height);
        Assert.False(Directory.Exists(root));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
