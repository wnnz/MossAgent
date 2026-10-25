using System.Security.Cryptography;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FilePreviewPaneAttachmentTests
{
    private static ToolExecutionContext Context(ApprovalPolicy policy = ApprovalPolicy.FullAccess) =>
        new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], policy);

    private static ConversationAttachment Snapshot(Guid taskId, string displayPath = "notes.txt",
        long byteCount = 64, string mediaType = "text/plain", Guid? messageId = null) =>
        new(Guid.NewGuid(), taskId, messageId ?? Guid.NewGuid(), displayPath, byteCount,
            Convert.ToHexString(SHA256.HashData("snapshot"u8)), mediaType);

    private static RecordingConversationAttachmentStore Store(params WorkspaceAttachmentContent[] contents) =>
        new() { ReadHandler = _ => Task.FromResult<IReadOnlyList<WorkspaceAttachmentContent>>(contents) };

    private static FilePreviewPaneViewModel Pane(RecordingConversationAttachmentStore? store,
        InspectorProbeToolExecutor? tools = null) =>
        new(tools ?? new InspectorProbeToolExecutor(), new PreviewProbeArtifactReader(), attachments: store);

    [Fact]
    public async Task TextAttachmentOpensReadOnlySnapshotThroughTheStoreWithoutTools()
    {
        var context = Context(ApprovalPolicy.ReadOnly) with { ToolsEnabled = false };
        var attachment = Snapshot(context.TaskId);
        var toolCalls = 0;
        var store = Store(new WorkspaceAttachmentContent("full/notes.txt", "notes.txt", "snapshot body", 64));
        using var pane = Pane(store, new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { toolCalls++; return Task.FromResult(ToolResult.Success("unused", string.Empty)); } });
        pane.Attach(context);
        var chip = new ChatMessageAttachmentViewModel("notes.txt", "64 B", Attachment: attachment);
        Assert.True(pane.OpenAttachmentCommand.CanExecute(chip));
        await pane.OpenAttachmentCommand.ExecuteAsync(chip);
        var tab = Assert.Single(pane.Tabs);
        Assert.Same(tab, pane.Active);
        Assert.True(pane.IsOpen);
        Assert.True(tab.IsReady);
        Assert.True(tab.IsStoredArtifact);
        Assert.Equal("snapshot body", tab.Text);
        Assert.Equal(InspectorText.Format("PreviewSize", 64 / 1024d), tab.Status);
        Assert.Equal("attachment:" + attachment.Id.ToString("N"), tab.Path);
        Assert.Equal("notes.txt", tab.DisplayPath);
        Assert.Equal([attachment.MessageId], store.ReadMessages);
        Assert.Equal(0, toolCalls);
        Assert.False(pane.CanReveal);
        Assert.False(pane.RevealCommand.CanExecute(null));
    }

    [Fact]
    public async Task PdfAttachmentShowsExtractedTextAndPageStats()
    {
        var context = Context();
        var document = AttachmentDocument.FromExtractedText(AttachmentDocument.PdfMediaType,
            new byte[] { 0x25, 0x50, 0x44, 0x46 }, 2, 5, 18, true);
        var attachment = Snapshot(context.TaskId, "report.pdf", 8, AttachmentDocument.PdfMediaType);
        var store = Store(new WorkspaceAttachmentContent("full/report.pdf", "report.pdf",
            "pdf extracted body", 8, Document: document));
        using var pane = Pane(store);
        pane.Attach(context);
        await pane.OpenAttachmentAsync(new ChatMessageAttachmentViewModel(
            "report.pdf", "8 B", KindLabel: "PDF", Attachment: attachment), Token);
        var tab = pane.Active!;
        Assert.True(tab.IsReady);
        Assert.True(tab.IsDocument);
        Assert.False(tab.IsImage);
        Assert.Equal("pdf extracted body", tab.Text);
        Assert.True(tab.IsDocumentTruncated);
        Assert.Equal(InspectorText.Format("PreviewPdfPages", 2, 5), tab.DocumentPageStatus);
        Assert.Equal(InspectorText.Format("PreviewSize", 8 / 1024d), tab.Status);
        Assert.False(pane.CanReveal);
    }

    [Fact]
    public async Task UnavailableSnapshotKeepsAnOpenTabAndRefreshReReadsTheStore()
    {
        var context = Context();
        var attachment = Snapshot(context.TaskId);
        var store = new RecordingConversationAttachmentStore
        {
            ReadHandler = _ => Task.FromException<IReadOnlyList<WorkspaceAttachmentContent>>(
                new ConversationAttachmentUnavailableException())
        };
        using var pane = Pane(store);
        pane.Attach(context);
        var chip = new ChatMessageAttachmentViewModel("notes.txt", "64 B", Attachment: attachment);
        await pane.OpenAttachmentAsync(chip, Token);
        var tab = Assert.Single(pane.Tabs);
        Assert.True(pane.IsOpen);
        Assert.False(tab.IsReady);
        Assert.False(tab.IsLoading);
        Assert.Equal(InspectorText.Get("PreviewAttachmentUnavailable"), tab.Status);
        store.ReadHandler = _ => Task.FromResult<IReadOnlyList<WorkspaceAttachmentContent>>(
            [new WorkspaceAttachmentContent("full/notes.txt", "notes.txt", "recovered", 9)]);
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.True(tab.IsReady);
        Assert.Equal("recovered", tab.Text);
        Assert.Equal([attachment.MessageId, attachment.MessageId], store.ReadMessages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForeignOrTamperedSnapshotsFailBeforeAnyStoreRead(bool otherTask)
    {
        var context = Context();
        var attachment = Snapshot(context.TaskId);
        var invalid = otherTask ? attachment with { TaskId = Guid.NewGuid() }
            : attachment with { Sha256 = "tampered" };
        var store = Store(new WorkspaceAttachmentContent("full/notes.txt", "notes.txt", "body", 4));
        using var pane = Pane(store);
        pane.Attach(context);
        await pane.OpenAttachmentAsync(new ChatMessageAttachmentViewModel(
            "notes.txt", "64 B", Attachment: invalid), Token);
        Assert.Equal(InspectorText.Get("PreviewAttachmentUnavailable"), pane.Active!.Status);
        Assert.Empty(store.ReadMessages);
    }

    [Fact]
    public async Task ReopeningDeduplicatesBySnapshotIdentityAndRefreshes()
    {
        var context = Context();
        var first = Snapshot(context.TaskId);
        var second = Snapshot(context.TaskId, "other.txt");
        var store = Store(new WorkspaceAttachmentContent("full", "notes.txt", "body", 4));
        using var pane = Pane(store);
        pane.Attach(context);
        var chip = new ChatMessageAttachmentViewModel("notes.txt", "64 B", Attachment: first);
        await pane.OpenAttachmentAsync(chip, Token);
        await pane.OpenAttachmentAsync(chip, Token);
        Assert.Single(pane.Tabs);
        Assert.Equal(2, store.ReadMessages.Count);
        await pane.OpenAttachmentAsync(new ChatMessageAttachmentViewModel(
            "other.txt", "32 B", Attachment: second), Token);
        Assert.Equal(2, pane.Tabs.Count);
        Assert.Distinct(pane.Tabs.Select(tab => tab.Path));
        Assert.All(pane.Tabs, tab => Assert.StartsWith("attachment:", tab.Path, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingAttachmentStoreFailsClosed()
    {
        var context = Context();
        var attachment = Snapshot(context.TaskId);
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor(), new PreviewProbeArtifactReader());
        pane.Attach(context);
        await pane.OpenAttachmentAsync(new ChatMessageAttachmentViewModel(
            "notes.txt", "64 B", Attachment: attachment), Token);
        var tab = Assert.Single(pane.Tabs);
        Assert.False(tab.IsReady);
        Assert.Equal(InspectorText.Get("PreviewFailed"), tab.Status);
    }

    [Fact]
    public async Task ChipsWithoutIdentityOrPreviewAreIgnored()
    {
        var store = Store();
        using var pane = Pane(store);
        Assert.False(pane.OpenAttachmentCommand.CanExecute(new ChatMessageAttachmentViewModel("a.txt", "4 B")));
        pane.Attach(Context());
        Assert.False(pane.OpenAttachmentCommand.CanExecute(new ChatMessageAttachmentViewModel("a.txt", "4 B")));
        Assert.False(pane.OpenAttachmentCommand.CanExecute(null));
        await pane.OpenAttachmentAsync(new ChatMessageAttachmentViewModel("a.txt", "4 B"), Token);
        Assert.Empty(pane.Tabs);
        Assert.False(pane.IsOpen);
        Assert.Empty(store.ReadMessages);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
