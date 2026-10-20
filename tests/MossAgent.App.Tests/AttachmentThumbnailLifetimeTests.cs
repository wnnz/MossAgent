using System.Buffers.Binary;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AttachmentThumbnailLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OversizedOrMismatchedPngOutputsNeverReachTheVisibleImage(bool forgedHeader) => _session.DispatchAsync(async () =>
    {
        var renderer = new ThumbnailTestRenderer();
        var control = new AppAttachmentThumbnail { Thumbnail = Source(renderer) };
        var window = new AppWindow { Content = control };
        window.Show();
        try
        {
            var bytes = Convert.FromBase64String(ThumbnailTestRenderer.Image.Base64Data);
            if (forgedHeader) BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), 8192);
            var result = AttachmentImage.FromDecodedBytes("image/png", bytes, forgedHeader ? 1 : 8192, 1);
            Assert.Single(renderer.Requests).SetResult(result);
            await control.Loading.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Dispatcher.UIThread.RunJobs();
            Assert.False(control.HasThumbnail);
            Assert.Null(control.Source);
        }
        finally { window.Close(); }
    }, Token);

    [Fact]
    public Task RemovingACompletedRequestClearsTheBitmapAndRestoresTheFallback() => _session.DispatchAsync(async () =>
    {
        var renderer = new ThumbnailTestRenderer();
        var chip = new AppAttachmentChip { DisplayPath = "image.png", Thumbnail = Source(renderer) };
        var window = new AppWindow { Content = chip };
        window.Show();
        try
        {
            var image = chip.FindControl<AppAttachmentThumbnail>("ThumbnailImage")!;
            renderer.Requests[0].SetResult(ThumbnailTestRenderer.Image);
            await image.Loading.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Dispatcher.UIThread.RunJobs();
            Assert.True(image.HasThumbnail);

            chip.Thumbnail = null;

            Assert.False(image.HasThumbnail);
            Assert.Null(image.Source);
            Assert.False(image.IsVisible);
            Assert.True(chip.FindControl<AppIcon>("FileIcon")!.IsVisible);
            Assert.True(renderer.Tokens[0].IsCancellationRequested);
        }
        finally { window.Close(); }
    }, Token);

    [Fact]
    public Task ReattachingAChipStartsANewReadWithoutReusingTheDetachedRequest() => _session.DispatchAsync(async () =>
    {
        var renderer = new ThumbnailTestRenderer();
        var chip = new AppAttachmentChip { DisplayPath = "image.png", Thumbnail = Source(renderer) };
        var window = new AppWindow { Content = chip };
        window.Show();
        try
        {
            var image = chip.FindControl<AppAttachmentThumbnail>("ThumbnailImage")!;
            var first = image.Loading;
            window.Content = null;
            Assert.True(renderer.Tokens[0].IsCancellationRequested);
            window.Content = chip;
            Assert.Equal(2, renderer.Requests.Count);
            renderer.Requests[1].SetResult(ThumbnailTestRenderer.Image);
            await image.Loading.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Dispatcher.UIThread.RunJobs();
            var current = image.Source;
            Assert.NotNull(current);
            renderer.Requests[0].SetResult(ThumbnailTestRenderer.Image);
            await first.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(current, image.Source);
        }
        finally { window.Close(); }
    }, Token);

    private static AttachmentThumbnailSource Source(ThumbnailTestRenderer renderer) => new(ThumbnailTestRenderer.Image, renderer);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
