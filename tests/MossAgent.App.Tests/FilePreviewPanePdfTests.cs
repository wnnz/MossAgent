using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FilePreviewPanePdfTests
{
    private static ToolExecutionContext Context() => new(Guid.NewGuid(),
        System.IO.Path.GetTempPath(), [System.IO.Path.GetTempPath()], ApprovalPolicy.ReadOnly);

    [Fact]
    public async Task PdfArtifactRendersExtractedTextWithTruncationAndPageStatus()
    {
        var extractor = new ProbeDocumentTextExtractor
        { Extract = (_, _) => Task.FromResult(new DocumentTextExtraction("extracted pdf body", 2, 5, true)) };
        using var pane = new FilePreviewPaneViewModel(
            new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(new ToolResult(true, "snapshot",
                Artifacts: [new ToolArtifact("report.pdf", "snapshot", "application/pdf", 64, "hash")])) },
            new PreviewProbeArtifactReader(), documents: extractor);
        pane.Attach(Context());
        await pane.OpenAsync("report.pdf", TestContext.Current.CancellationToken);
        var tab = pane.Active!;
        Assert.True(tab.IsReady);
        Assert.True(tab.IsDocument);
        Assert.True(tab.IsDocumentTruncated);
        Assert.Equal("extracted pdf body", tab.Text);
        Assert.Equal(InspectorText.Format("PreviewPdfPages", 2, 5), tab.DocumentPageStatus);
        Assert.Equal(1, extractor.Calls);
    }

    [Fact]
    public async Task EncryptedPdfShowsActionableLocalizedStatus()
    {
        var extractor = new ProbeDocumentTextExtractor
        { Extract = (_, _) => throw new AttachmentDocumentValidationException(AttachmentDocumentFailure.Encrypted) };
        using var pane = new FilePreviewPaneViewModel(
            new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(new ToolResult(true, "snapshot",
                Artifacts: [new ToolArtifact("locked.pdf", "snapshot", "application/pdf", 64, "hash")])) },
            new PreviewProbeArtifactReader(), documents: extractor);
        pane.Attach(Context());
        await pane.OpenAsync("locked.pdf", TestContext.Current.CancellationToken);
        var tab = pane.Active!;
        Assert.False(tab.IsReady);
        Assert.Equal(InspectorText.Get("PreviewPdfEncrypted"), tab.Status);
    }
}
