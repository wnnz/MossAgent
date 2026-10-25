using Avalonia.Controls;
using Avalonia.Headless;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using System.Security.Cryptography;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FilePreviewPaneAttachmentVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task ImageAttachmentOpensAZoomableBitmapTabFromTheSnapshot() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var image = ThumbnailTestRenderer.Image;
        var attachment = Snapshot(context.TaskId, "pic.png", image.ByteCount, image.MediaType);
        var store = new RecordingConversationAttachmentStore
        {
            ReadHandler = _ => Task.FromResult<IReadOnlyList<WorkspaceAttachmentContent>>(
                [new WorkspaceAttachmentContent("full/pic.png", "pic.png", "", image.ByteCount, image)])
        };
        using var pane = new FilePreviewPaneViewModel(
            new InspectorProbeToolExecutor(), new PreviewProbeArtifactReader(), attachments: store);
        pane.Attach(context);
        await pane.OpenAttachmentAsync(new ChatMessageAttachmentViewModel(
            "pic.png", ChatMessageAttachmentViewModel.FormatSize(image.ByteCount), Attachment: attachment), Token);
        var tab = pane.Active!;
        Assert.True(tab.IsReady);
        Assert.True(tab.IsImage);
        Assert.NotNull(tab.Image);
        Assert.False(tab.IsDocument);
        Assert.Equal(InspectorText.Format("PreviewSize", image.ByteCount / 1024d), tab.Status);
        Assert.Equal([attachment.MessageId], store.ReadMessages);
        Assert.False(pane.CanReveal);
    }, Token);

    [Fact]
    public Task IdentitylessThumbnailOpensThroughTheRenderedPreviewAndDeduplicates() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var store = new RecordingConversationAttachmentStore();
        var loads = 0;
        var source = new AttachmentThumbnailSource(_ =>
        {
            loads++;
            return Task.FromResult(ThumbnailTestRenderer.Image);
        });
        using var pane = new FilePreviewPaneViewModel(
            new InspectorProbeToolExecutor(), new PreviewProbeArtifactReader(), attachments: store);
        pane.Attach(context);
        var chip = new ChatMessageAttachmentViewModel("pic.png", "43 B", source);
        await pane.OpenAttachmentAsync(chip, Token);
        var tab = pane.Active!;
        Assert.True(tab.IsReady);
        Assert.True(tab.IsImage);
        Assert.NotNull(tab.Image);
        Assert.Equal(InspectorText.Format("PreviewSize", ThumbnailTestRenderer.Image.ByteCount / 1024d), tab.Status);
        Assert.Empty(store.ReadMessages);
        Assert.Equal(1, loads);
        await pane.OpenAttachmentAsync(chip, Token);
        Assert.Single(pane.Tabs);
        Assert.Equal(2, loads);
    }, Token);

    [Fact]
    public Task ChipOpenCommandMakesTheBodyClickableWhileRemoveStaysIndependent() => _session.Dispatch(() =>
    {
        var opened = new List<object?>();
        var parameter = new object();
        var removed = 0;
        var chip = new AppAttachmentChip
        {
            DisplayPath = "pic.png",
            SizeLabel = "43 B",
            OpenCommand = new RelayCommand<object?>(item => opened.Add(item)),
            OpenCommandParameter = parameter,
            RemoveCommand = new RelayCommand(() => removed++)
        };
        var window = new AppWindow { Content = chip };
        window.Show();
        try
        {
            window.UpdateLayout();
            var open = chip.FindControl<AppButton>("OpenButton")!;
            Assert.True(chip.HasOpenAction);
            Assert.True(open.IsVisible);
            Assert.False(string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(open)));
            open.Command!.Execute(open.CommandParameter);
            Assert.Same(parameter, Assert.Single(opened));
            Assert.Equal(0, removed);
            chip.OpenCommand = null;
            Assert.False(chip.HasOpenAction);
            Assert.False(open.IsVisible);
            var remove = chip.FindControl<AppIconButton>("RemoveButton")!;
            Assert.True(remove.IsVisible);
            remove.Command!.Execute(null);
            Assert.Equal(1, removed);
        }
        finally { window.Close(); }
    }, Token);

    private static ToolExecutionContext Context() =>
        new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess);

    private static ConversationAttachment Snapshot(Guid taskId, string displayPath, long byteCount, string mediaType) =>
        new(Guid.NewGuid(), taskId, Guid.NewGuid(), displayPath, byteCount,
            Convert.ToHexString(SHA256.HashData("snapshot"u8)), mediaType);

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
