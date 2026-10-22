using System.Text;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceDocumentAttachmentHistoryTests
{
    [Fact]
    public async Task PdfCountBudgetOmitsWholeOlderAttachmentsAndKeepsLatestFiles()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var store = new ConversationAttachmentStore(paths);
        var document = Pdf();
        var older = await SaveAsync(fixture, store, document, 3, "older documents");
        var latest = await SaveAsync(fixture, store, document, 2, "latest documents");
        var model = state.Model with { SupportsImages = false, SupportsFiles = true };

        var messages = await new WorkspaceConversationHistoryBuilder(store)
            .BuildAsync(state.Task, model, [older, latest], fixture.Token);

        Assert.Contains("omitted because of the model context budget", messages[0].Content);
        Assert.True(messages[0].Files.IsDefaultOrEmpty);
        Assert.Equal(2, messages[1].Files.Length);
        Assert.Equal(document.Base64Data, messages[1].Files[0].Base64Data);
        Assert.Equal("latest documents", messages[1].Content);
        Assert.DoesNotContain(document.Base64Data, messages[1].Content);
        ModelFileRequestValidation.Validate(new(state.Provider, model, messages, []));
    }

    [Fact]
    public async Task PdfHistoryReopensFromSnapshotsAfterOriginalSourcesAreDeleted()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var store = new ConversationAttachmentStore(paths);
        var source = Path.Combine(fixture.Project.PrimaryDirectory, "source-to-delete.pdf");
        await File.WriteAllBytesAsync(source,
            Convert.FromBase64String(Pdf().Base64Data), fixture.Token);
        var reader = new WorkspaceAttachmentReader();
        var selected = await reader.ReadAsync(fixture.Project, [source], fixture.Token);
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User,
            "read this PDF", DateTimeOffset.UtcNow);
        message = message with
        {
            Attachments = await store.SaveAsync(fixture.Task.Id, message.Id, selected, fixture.Token)
        };
        File.Delete(source);
        var model = state.Model with { SupportsImages = false, SupportsFiles = true };

        var messages = await new WorkspaceConversationHistoryBuilder(store)
            .BuildAsync(state.Task, model, [message], fixture.Token);

        var file = Assert.Single(Assert.Single(messages).Files);
        Assert.Equal("source-to-delete.pdf", file.FileName);
        Assert.Equal(AttachmentDocument.PdfMediaType, file.MediaType);
    }

    private static async Task<ConversationMessage> SaveAsync(WorkspacePersistenceFixture fixture,
        ConversationAttachmentStore store, AttachmentDocument document, int count, string text)
    {
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User,
            text, DateTimeOffset.UtcNow);
        var contents = Enumerable.Range(0, count).Select(index => new WorkspaceAttachmentContent(
            "never-read.pdf", $"document-{index}.pdf", "", document.ByteCount, Document: document)).ToArray();
        return message with
        {
            Attachments = await store.SaveAsync(fixture.Task.Id, message.Id, contents, fixture.Token)
        };
    }

    private static AttachmentDocument Pdf() =>
        AttachmentDocument.FromDecodedBytes(AttachmentDocument.PdfMediaType,
            Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF\n"));
}
