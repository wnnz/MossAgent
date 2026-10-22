using System.Text;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class ModelFileContextTests
{
    [Fact]
    public void EstimateIncludesFilesAsIndivisibleMetadata()
    {
        var file = File(900);
        var plain = new ModelMessage(ModelRole.User, "text");

        Assert.Equal(file.ContextCharacterCost,
            ModelContextWindowBuilder.Estimate([plain with { Files = [file] }])
            - ModelContextWindowBuilder.Estimate([plain]));
    }

    [Fact]
    public void LatestTextCanBeTrimmedWithoutChangingPdfBytes()
    {
        var file = File(68);
        var original = new ModelMessage(ModelRole.User, new string('x', 10_000), Files: [file]);

        var result = Assert.Single(ModelContextWindowBuilder.Build([original], [], Model()));

        Assert.Same(file, Assert.Single(result.Files));
        Assert.True(result.Content.Length < original.Content.Length);
        Assert.InRange(ModelContextWindowBuilder.Estimate([result]), file.ContextCharacterCost, 1_536);
        Assert.Equal(10_000, original.Content.Length);
    }

    [Fact]
    public void OversizedLatestPdfFailsInsteadOfSlicingOrDroppingBytes()
    {
        var file = File(6_000);
        var message = new ModelMessage(ModelRole.User, "describe", Files: [file]);

        Assert.Throws<InvalidOperationException>(() =>
            ModelContextWindowBuilder.Build([message], [], Model()));
        Assert.Equal(6_000, Convert.FromBase64String(file.Base64Data).Length);
    }

    private static ModelFile File(int byteCount)
    {
        var bytes = new byte[byteCount];
        Encoding.ASCII.GetBytes("%PDF-1.7\n").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("%%EOF\n").CopyTo(bytes, byteCount - 6);
        var document = AttachmentDocument.FromDecodedBytes(AttachmentDocument.PdfMediaType, bytes);
        return ModelFile.FromAttachment("context.pdf", document);
    }

    private static ModelProfile Model() => new(Guid.NewGuid(), Guid.NewGuid(), "fixture", "fixture",
        1_024, 128, "off", null, null, false, true, true, true, "{}");
}
