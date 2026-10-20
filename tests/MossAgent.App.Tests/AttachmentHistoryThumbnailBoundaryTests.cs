using System.Security.Cryptography;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AttachmentHistoryThumbnailBoundaryTests
{
    [Fact]
    public async Task MappingIsLazyAndOnlyLoadsTheRequestedMessageSnapshot()
    {
        var (message, attachment, content) = Create();
        var store = new RecordingConversationAttachmentStore
        {
            ReadHandler = _ => Task.FromResult<IReadOnlyList<WorkspaceAttachmentContent>>([content])
        };
        var factory = new ConversationAttachmentThumbnailFactory(store, new SkiaAttachmentThumbnailRenderer());
        var row = Assert.Single(WorkspaceConversationMapper.ToChatMessage(message, factory).Attachments);
        Assert.Empty(store.ReadMessages);

        var result = await Assert.IsType<AttachmentThumbnailSource>(row.Thumbnail).LoadAsync(Token);

        Assert.Equal([message.Id], store.ReadMessages);
        Assert.Equal(attachment.DisplayPath, row.DisplayPath);
        Assert.Equal("image/png", result.MediaType);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnotherTaskOrMessageIsRejectedBeforeReading(bool otherTask)
    {
        var (message, attachment, _) = Create();
        var store = new RecordingConversationAttachmentStore();
        var factory = new ConversationAttachmentThumbnailFactory(store, new ThumbnailTestRenderer());
        var invalid = otherTask ? attachment with { TaskId = Guid.NewGuid() }
            : attachment with { MessageId = Guid.NewGuid() };
        var source = Assert.IsType<AttachmentThumbnailSource>(factory.Create(message, invalid));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => source.LoadAsync(Token));

        Assert.Empty(store.ReadMessages);
    }

    [Theory]
    [InlineData(MessageRole.Assistant)]
    [InlineData(MessageRole.System)]
    [InlineData(MessageRole.Tool)]
    public void NonUserMessagesAndTextNeverCreateImageReads(MessageRole role)
    {
        var (message, attachment, _) = Create();
        var store = new RecordingConversationAttachmentStore();
        var factory = new ConversationAttachmentThumbnailFactory(store, new ThumbnailTestRenderer());

        Assert.Null(factory.Create(message with { Role = role }, attachment));
        Assert.Null(factory.Create(message, attachment with { MediaType = "text/plain" }));
        Assert.Empty(store.ReadMessages);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task InvalidSnapshotContentFailsWithoutRenderingAndDoesNotPoisonLaterReads(int kind)
    {
        var (message, attachment, content) = Create();
        IReadOnlyList<WorkspaceAttachmentContent> invalid = kind switch
        {
            0 => [],
            1 => [new("file.txt", "file.txt", "text", 4)],
            _ => [content, content]
        };
        var store = new RecordingConversationAttachmentStore { ReadHandler = _ => Task.FromResult(invalid) };
        var renderer = new ThumbnailTestRenderer();
        var source = new ConversationAttachmentThumbnailFactory(store, renderer).Create(message, attachment)!;

        await Assert.ThrowsAsync<InvalidDataException>(() => source.LoadAsync(Token));
        Assert.Empty(renderer.Requests);
        store.ReadHandler = _ => Task.FromResult<IReadOnlyList<WorkspaceAttachmentContent>>([content]);
        var retry = source.LoadAsync(Token);
        var rendered = ThumbnailTestRenderer.Image;
        Assert.Single(renderer.Requests).SetResult(rendered);
        Assert.Same(rendered, await retry);
    }

    [Fact]
    public async Task CancellationRetainsTheReadGateUntilAnIgnoringStoreActuallyReturns()
    {
        var (message, attachment, content) = Create();
        var completion = new TaskCompletionSource<IReadOnlyList<WorkspaceAttachmentContent>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new RecordingConversationAttachmentStore { ReadHandler = _ => completion.Task };
        var source = new ConversationAttachmentThumbnailFactory(store, new SkiaAttachmentThumbnailRenderer())
            .Create(message, attachment)!;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var first = source.LoadAsync(cancellation.Token);
        var second = source.LoadAsync(Token);
        Assert.Single(store.ReadMessages);
        await cancellation.CancelAsync();
        Assert.True(store.ReadToken.IsCancellationRequested);
        Assert.False(second.IsCompleted);
        Assert.Single(store.ReadMessages);
        completion.SetResult([content]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(1, (await second.WaitAsync(TimeSpan.FromSeconds(5), Token)).Width);
        Assert.Equal(2, store.ReadMessages.Count);
    }

    [Fact]
    public async Task AlreadyCancelledRequestNeverReadsAndDoesNotPreventTheNextRequest()
    {
        var (message, attachment, content) = Create();
        var store = new RecordingConversationAttachmentStore
        {
            ReadHandler = _ => Task.FromResult<IReadOnlyList<WorkspaceAttachmentContent>>([content])
        };
        var source = new ConversationAttachmentThumbnailFactory(store, new SkiaAttachmentThumbnailRenderer())
            .Create(message, attachment)!;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.LoadAsync(cancellation.Token));
        Assert.Empty(store.ReadMessages);
        Assert.Equal(1, (await source.LoadAsync(Token)).Height);
        Assert.Single(store.ReadMessages);
    }

    private static (ConversationMessage Message, ConversationAttachment Attachment, WorkspaceAttachmentContent Content) Create()
    {
        var image = ThumbnailTestRenderer.Image;
        var message = new ConversationMessage(Guid.NewGuid(), Guid.NewGuid(), MessageRole.User, "keep this text", DateTimeOffset.UtcNow);
        var attachment = new ConversationAttachment(Guid.NewGuid(), message.TaskId, message.Id, "image.png", image.ByteCount,
            Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(image.Base64Data))), image.MediaType);
        return (message with { Attachments = [attachment] }, attachment, new("never-read.png", "image.png", "", image.ByteCount, image));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
