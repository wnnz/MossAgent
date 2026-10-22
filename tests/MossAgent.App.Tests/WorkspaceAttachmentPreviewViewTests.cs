using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceAttachmentPreviewViewTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526)]
    [InlineData(true, 980)]
    public Task FullImageUsesSharedViewerZoomAndEscapeClose(bool dark, int width) =>
        _session.DispatchAsync(async () =>
        {
            using var preview = new AttachmentPreviewViewModel();
            await preview.OpenAsync(ValidSource("stored.png"), Token);
            var view = new WorkspaceAttachmentPreviewView { DataContext = preview };
            var window = Window(view, dark, width);
            window.Show();
            try
            {
                Flush(window);
                var dialog = Assert.Single(view.GetVisualDescendants().OfType<AppSettingsDialog>());
                var image = Assert.Single(view.GetVisualDescendants().OfType<AppImagePreview>());
                Assert.True(dialog.IsVisible);
                Assert.Single(image.GetVisualDescendants().OfType<AppImage>());
                Assert.Contains(view.GetVisualDescendants().OfType<AppIconButton>(),
                    button => button.Label == InspectorText.PreviewZoomIn);
                SaveEvidence(window, dark, width);
                preview.ZoomInCommand.Execute(null);
                Flush(window);
                Assert.False(preview.IsFit);
                var close = Assert.Single(view.GetVisualDescendants().OfType<AppIconButton>(),
                    button => button.Label == AttachmentChipText.ClosePreviewLabel);
                Assert.True(close.Focus());
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Flush(window);
                Assert.False(preview.IsOpen);
                Assert.False(dialog.IsVisible);
            }
            finally { window.Close(); }
        }, Token);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CorruptSnapshotShowsSafeStateWithoutAnImage(bool dark) =>
        _session.DispatchAsync(async () =>
        {
            using var preview = new AttachmentPreviewViewModel();
            var valid = Convert.FromBase64String(ThumbnailTestRenderer.Image.Base64Data);
            var corrupt = AttachmentImage.FromDecodedBytes("image/png", new byte[valid.Length], 1, 1);
            await preview.OpenAsync(new AttachmentPreviewSource("corrupt.png", "68 B", corrupt, null), Token);
            var view = new WorkspaceAttachmentPreviewView { DataContext = preview };
            var window = Window(view, dark, 526);
            window.Show();
            try
            {
                Flush(window);
                Assert.False(preview.HasImage);
                Assert.Contains("无法显示此附件快照", preview.Status);
                Assert.Single(view.GetVisualDescendants().OfType<AppText>(),
                    text => text.Text == preview.Status);
            }
            finally { window.Close(); }
        }, Token);

    private static AttachmentPreviewSource ValidSource(string name) =>
        new(name, "68 B", ThumbnailTestRenderer.Image, null);

    private static AppWindow Window(Control content, bool dark, int width) => new()
    {
        Width = width,
        Height = 601,
        Content = content,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void SaveEvidence(AppWindow window, bool dark, int width)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_ATTACHMENT_PREVIEW_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"attachment-preview-{dark}-{width}.png"),
            PngBitmapEncoderOptions.Default);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
