using Avalonia.Headless;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AttachmentPreviewViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task ValidSnapshotLoadsZoomAndClosesWithoutLeakingBitmap() => _session.DispatchAsync(async () =>
    {
        using var preview = new AttachmentPreviewViewModel();
        var image = ThumbnailTestRenderer.Image;
        var source = new AttachmentPreviewSource("stored.png", "68 B", image, null);

        await preview.OpenAsync(source, Token);

        Assert.True(preview.IsOpen);
        Assert.True(preview.HasImage);
        Assert.False(preview.IsLoading);
        Assert.NotNull(preview.Image);
        Assert.Equal(1, preview.Image!.PixelSize.Width);
        Assert.Equal(1, preview.Image.PixelSize.Height);
        Assert.Contains("1 × 1", preview.Dimensions);
        preview.ZoomInCommand.Execute(null);
        Assert.False(preview.IsFit);
        Assert.Equal(1.25, preview.Scale);
        preview.FitCommand.Execute(null);
        Assert.True(preview.IsFit);
        preview.Close();
        Assert.False(preview.IsOpen);
        Assert.Null(preview.Image);
        Assert.False(preview.HasImage);
    }, Token);

    [Fact]
    public Task CorruptSnapshotFailsClosedWithoutPublishingAnImage() => _session.DispatchAsync(async () =>
    {
        using var preview = new AttachmentPreviewViewModel();
        var valid = Convert.FromBase64String(ThumbnailTestRenderer.Image.Base64Data);
        var corrupt = AttachmentImage.FromDecodedBytes("image/png", new byte[valid.Length], 1, 1);
        var source = new AttachmentPreviewSource("corrupt.png", "68 B", corrupt, null);

        await preview.OpenAsync(source, Token);

        Assert.True(preview.IsOpen);
        Assert.False(preview.HasImage);
        Assert.False(preview.IsLoading);
        Assert.True(preview.IsStateVisible);
        Assert.Equal(WorkspaceText.Get("AttachmentPreviewFailed"), preview.Status);
    }, Token);

    [Fact]
    public Task TimedOutSourceIsCancelledAndRetryCanReplaceIt() => _session.DispatchAsync(async () =>
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<AttachmentImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = new AttachmentPreviewSource("slow.png", "68 B", _ =>
        {
            entered.TrySetResult();
            return pending.Task;
        }, null);
        using var preview = new AttachmentPreviewViewModel(TimeSpan.FromMilliseconds(100));

        var opening = preview.OpenAsync(slow, Token);
        await entered.Task.WaitAsync(Token);
        await opening;

        Assert.True(preview.IsOpen);
        Assert.False(preview.HasImage);
        Assert.Equal(WorkspaceText.Get("AttachmentPreviewTimedOut"), preview.Status);
        var replacement = new AttachmentPreviewSource("replacement.png", "68 B", ThumbnailTestRenderer.Image, null);
        await preview.OpenAsync(replacement, Token);
        pending.TrySetResult(ThumbnailTestRenderer.Image);
        Assert.True(preview.HasImage);
        Assert.Equal("replacement.png", preview.DisplayPath);
    }, Token);

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
