using MossAgent.Application.Attachments;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ConversationDocumentAttachmentValidationTests
{
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid MessageId = Guid.NewGuid();

    [Fact]
    public void AggregateDocumentBudgetIsEnforcedSeparatelyFromImages()
    {
        var hash = new string('a', 64);
        var pdf = new ConversationAttachment(Guid.NewGuid(), TaskId, MessageId, "a.pdf",
            AttachmentDocument.MaximumByteCount, hash, AttachmentDocument.PdfMediaType);
        Assert.Null(Record.Exception(() => ConversationAttachmentValidation.Validate(TaskId, MessageId,
            [pdf, pdf with { Id = Guid.NewGuid() }])));
        Assert.Throws<InvalidDataException>(() => ConversationAttachmentValidation.Validate(TaskId, MessageId,
            [pdf, pdf with { Id = Guid.NewGuid() }, pdf with { Id = Guid.NewGuid() }]));
        var image = new ConversationAttachment(Guid.NewGuid(), TaskId, MessageId, "a.png",
            AttachmentImage.MaximumByteCount, hash, "image/png");
        Assert.Null(Record.Exception(() => ConversationAttachmentValidation.Validate(TaskId, MessageId,
            [pdf, pdf with { Id = Guid.NewGuid() }, image, image with { Id = Guid.NewGuid() }])));
    }

    [Fact]
    public void PdfsDoNotRequireImageCapableModelsButImagesStillDo()
    {
        var model = new ModelProfile(Guid.NewGuid(), Guid.NewGuid(), "model", "Model", 8192, 1024,
            "off", null, null, false, false, true, true, "{}");
        var pdf = new WorkspaceAttachmentContent("a.pdf", "a.pdf", "text", 64,
            Document: AttachmentDocument.FromExtractedText(AttachmentDocument.PdfMediaType,
                AttachmentDocumentTestData.Pdf("x"), 1, 1, 1, false));
        ConversationAttachmentValidation.ValidateModel(model, [pdf]);
        var image = new WorkspaceAttachmentContent("a.png", "a.png", string.Empty, 64,
            Image: AttachmentImage.FromDecodedBytes("image/png",
                Convert.FromBase64String(AttachmentImageTestData.Png), 1, 1));
        Assert.Throws<ArgumentException>(() => ConversationAttachmentValidation.ValidateModel(model, [image]));
    }
}
