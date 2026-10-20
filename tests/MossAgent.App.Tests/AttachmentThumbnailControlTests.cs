using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AttachmentThumbnailControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task LoadedThumbnailKeepsChipGeometryAndAccessibleFileDetails(bool dark, bool removable) =>
        _session.DispatchAsync(async () =>
        {
            var renderer = new ThumbnailTestRenderer();
            var removed = 0;
            var chip = Chip(renderer);
            chip.RemoveCommand = removable ? new RelayCommand(() => removed++) : null;
            var window = new AppWindow { Width = 240, Height = 120, Content = chip,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            var image = chip.FindControl<AppAttachmentThumbnail>("ThumbnailImage")!;
            window.Show();
            try
            {
                window.UpdateLayout();
                var before = chip.Bounds;
                Assert.False(image.HasThumbnail);
                Assert.True(chip.FindControl<AppIcon>("FileIcon")!.IsVisible);
                Assert.Single(renderer.Requests).SetResult(ThumbnailTestRenderer.Image);
                await image.Loading.WaitAsync(TimeSpan.FromSeconds(5), Token);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(image.HasThumbnail);
                Assert.Equal(new PixelSize(1, 1), Assert.IsType<Bitmap>(image.Source).PixelSize);
                Assert.Equal(new Size(28, 28), image.Bounds.Size);
                Assert.Equal(before, chip.Bounds);
                Assert.False(chip.FindControl<AppIcon>("FileIcon")!.IsVisible);
                Assert.Contains(chip.DisplayPath, AutomationProperties.GetName(chip));
                Assert.Contains(chip.SizeLabel, AutomationProperties.GetName(chip));
                var remove = chip.FindControl<AppIconButton>("RemoveButton")!;
                Assert.Equal(removable, remove.IsVisible);
                if (removable) remove.Command!.Execute(null);
                Assert.Equal(removable ? 1 : 0, removed);
                SaveEvidence(window, dark, removable);
            }
            finally { window.Close(); }
            Assert.False(image.HasThumbnail);
            Assert.Null(image.Source);
            Assert.True(Assert.Single(renderer.Tokens).IsCancellationRequested);
        }, Token);

    [Fact]
    public Task DetachCancelsWorkAndLateCompletionCannotRestoreTheImage() => _session.DispatchAsync(async () =>
    {
        var renderer = new ThumbnailTestRenderer();
        var chip = Chip(renderer);
        var window = new AppWindow { Content = chip };
        window.Show();
        var image = chip.FindControl<AppAttachmentThumbnail>("ThumbnailImage")!;
        var loading = image.Loading;
        window.Close();
        Assert.True(Assert.Single(renderer.Tokens).IsCancellationRequested);
        Assert.Single(renderer.Requests).SetResult(ThumbnailTestRenderer.Image);
        await loading.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Dispatcher.UIThread.RunJobs();
        Assert.False(image.HasThumbnail);
        Assert.Null(image.Source);
    }, Token);

    [Fact]
    public Task ReplacingTheRequestCancelsThePreviousReadAndIgnoresItsLateResult() => _session.DispatchAsync(async () =>
    {
        var renderer = new ThumbnailTestRenderer();
        var chip = Chip(renderer);
        var window = new AppWindow { Content = chip };
        window.Show();
        try
        {
            var image = chip.FindControl<AppAttachmentThumbnail>("ThumbnailImage")!;
            var first = image.Loading;
            chip.Thumbnail = new AttachmentThumbnailSource(ThumbnailTestRenderer.Image, renderer);
            Assert.True(renderer.Tokens[0].IsCancellationRequested);
            Assert.Equal(2, renderer.Requests.Count);
            renderer.Requests[1].SetResult(ThumbnailTestRenderer.Image);
            await image.Loading.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Dispatcher.UIThread.RunJobs();
            var current = Assert.IsType<Bitmap>(image.Source);
            renderer.Requests[0].SetResult(ThumbnailTestRenderer.Image);
            await first.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(current, image.Source);
            Assert.True(image.HasThumbnail);
        }
        finally { window.Close(); }
    }, Token);

    [Fact]
    public Task RendererFailureFallsBackToTheFileIconWithoutLosingTheLabelOrRemoval() => _session.DispatchAsync(async () =>
    {
        var renderer = new ThumbnailTestRenderer();
        var chip = Chip(renderer);
        var removed = false;
        chip.RemoveCommand = new RelayCommand(() => removed = true);
        var window = new AppWindow { Content = chip };
        window.Show();
        try
        {
            var image = chip.FindControl<AppAttachmentThumbnail>("ThumbnailImage")!;
            Assert.Single(renderer.Requests).SetException(new IOException("fixture decoding failure"));
            await image.Loading.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Dispatcher.UIThread.RunJobs();
            Assert.False(image.HasThumbnail);
            Assert.True(chip.FindControl<AppIcon>("FileIcon")!.IsVisible);
            Assert.Equal("image.png", chip.FindControl<AppText>("PathLabel")!.Text);
            chip.FindControl<AppIconButton>("RemoveButton")!.Command!.Execute(null);
            Assert.True(removed);
        }
        finally { window.Close(); }
    }, Token);

    [Fact]
    public Task UnattachedChipsDoNotStartBackgroundReads() => _session.Dispatch(() =>
    {
        var renderer = new ThumbnailTestRenderer();
        var chip = Chip(renderer);
        Assert.Empty(renderer.Requests);
        Assert.False(chip.FindControl<AppAttachmentThumbnail>("ThumbnailImage")!.HasThumbnail);
    }, Token);

    private static AppAttachmentChip Chip(ThumbnailTestRenderer renderer) => new()
    {
        DisplayPath = "image.png", SizeLabel = "72 B",
        Thumbnail = new AttachmentThumbnailSource(ThumbnailTestRenderer.Image, renderer)
    };

    private static void SaveEvidence(AppWindow window, bool dark, bool removable)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_THUMBNAIL_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"thumbnail-dark-{dark}-remove-{removable}.png"), PngBitmapEncoderOptions.Default);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
