using System.Text;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class DocumentDraftAttachmentTests
{
    [Fact]
    public async Task PdfDraftReopensWithoutOriginalSourceOrDatabasePayloadColumns()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var document = Pdf("alpha");
        var source = Path.Combine(fixture.Project.PrimaryDirectory, "deleted-source.pdf");
        var original = new ConversationDraftAttachment(Guid.NewGuid(), source, "selected.pdf", null,
            document.ByteCount, WorkspaceAttachmentAuthorization.Create(fixture.Project),
            document.MediaType, Document: document);
        var repository = new SqliteConversationDraftRepository(
            fixture.Connections, new AppDataPaths(fixture.Project.PrimaryDirectory));

        Assert.True(await repository.SaveAsync(
            [new(scope, "read the PDF", 0, [original])], fixture.Token));

        var restored = (await new SqliteConversationDraftRepository(
            fixture.Connections, new AppDataPaths(fixture.Project.PrimaryDirectory))
            .GetAsync(scope, fixture.Token))!;
        var item = Assert.Single(restored.AttachmentItems);

        Assert.Equal("read the PDF", restored.Text);
        Assert.Equal(document.MediaType, item.MediaType);
        Assert.Null(item.Content);
        Assert.Equal(document.Base64Data, item.Document?.Base64Data);
        Assert.False(File.Exists(source));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM pragma_table_info('conversation_draft_attachments') "
            + "WHERE name IN ('content','image','document','base64_data');"));
    }

    private static AttachmentDocument Pdf(string marker) =>
        AttachmentDocument.FromDecodedBytes(AttachmentDocument.PdfMediaType,
            Encoding.ASCII.GetBytes($"%PDF-1.7\n%{marker}\n1 0 obj\n<<>>\nendobj\n%%EOF\n"));
}
