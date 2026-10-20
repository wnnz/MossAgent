using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceImageAttachmentHistoryTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageCountAndContextBudgetOmitWholeOlderAttachmentsWithAnExplicitMarker(bool contextLimit)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var store = new ConversationAttachmentStore(paths, images: new SkiaAttachmentImageDecoder());
        var image = await new SkiaAttachmentImageDecoder().DecodeAsync(Convert.FromBase64String(Png), fixture.Token);
        var count = contextLimit ? 1 : 6;
        var older = await SaveAsync(fixture, store, image, count, "older image prompt");
        var latest = await SaveAsync(fixture, store, image, count, "latest image prompt");
        var model = state.Model with { SupportsImages = true, ContextLength = contextLimit ? 1600 : 8192 };
        var messages = await new WorkspaceConversationHistoryBuilder(store).BuildAsync(state.Task, model, [older, latest], fixture.Token);
        Assert.Contains("omitted because of the model context budget", messages[0].Content);
        Assert.True(messages[0].Images.IsDefaultOrEmpty);
        Assert.Equal(count, messages[1].Images.Length);
        Assert.Equal("latest image prompt", messages[1].Content);
        Assert.All(messages[1].Images, payload => Assert.Equal(Png, payload.Base64Data));
        Assert.DoesNotContain("workspace_attachments", messages[1].Content);
        Assert.DoesNotContain(Png, messages[1].Content);
        Assert.Equal("older image prompt", older.Content);
        Assert.Equal(count, older.Attachments!.Count);
    }

    [Fact]
    public async Task LatestImagesAreNeverSilentlyDroppedToFitASmallBudget()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var store = new ConversationAttachmentStore(paths, images: new SkiaAttachmentImageDecoder());
        var image = await new SkiaAttachmentImageDecoder().DecodeAsync(Convert.FromBase64String(Png), fixture.Token);
        var latest = await SaveAsync(fixture, store, image, 1, "");
        var model = state.Model with { SupportsImages = true, ContextLength = 1024 };
        var messages = await new WorkspaceConversationHistoryBuilder(store).BuildAsync(state.Task, model, [latest], fixture.Token);
        Assert.Equal(Png, Assert.Single(Assert.Single(messages).Images).Base64Data);
        Assert.Empty(messages[0].Content);
        ModelImageRequestValidation.Validate(new(state.Provider, model, messages, []));
    }

    private static async Task<ConversationMessage> SaveAsync(WorkspacePersistenceFixture fixture, ConversationAttachmentStore store,
        AttachmentImage image, int count, string text)
    {
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, text, DateTimeOffset.UtcNow);
        var contents = Enumerable.Range(0, count).Select(index =>
            new WorkspaceAttachmentContent("never-read.png", $"image-{index}.png", "", image.ByteCount, image)).ToArray();
        return message with { Attachments = await store.SaveAsync(fixture.Task.Id, message.Id, contents, fixture.Token) };
    }
}
