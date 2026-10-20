using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AttachmentHistoryThumbnailControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public Task HistoryBindingDisplaysAReadOnlyThumbnailAndReleasesItOnDetach(bool dark, int unavailable) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
            var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
            var store = new ConversationAttachmentStore(paths, images: new SkiaAttachmentImageDecoder());
            var image = ThumbnailTestRenderer.Image;
            var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "保留图片说明", DateTimeOffset.UtcNow);
            message = message with { Attachments = await store.SaveAsync(message.TaskId, message.Id,
                [new("never-read.png", "history.png", "", image.ByteCount, image)], Token) };
            if (unavailable != 0)
            {
                var snapshot = Assert.Single(Directory.GetFiles(paths.GetTaskDirectory(message.TaskId), "*.txt", SearchOption.AllDirectories));
                if (unavailable == 1) await File.WriteAllBytesAsync(snapshot, new byte[image.ByteCount], Token);
                else File.Delete(snapshot);
            }
            var mapped = WorkspaceConversationMapper.ToChatMessage(message,
                new ConversationAttachmentThumbnailFactory(store, new SkiaAttachmentThumbnailRenderer()));
            var view = new WorkspaceMessageView { DataContext = mapped };
            var window = new AppWindow { Width = 526, Height = 240, Content = view,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                window.UpdateLayout();
                var chip = Assert.Single(view.GetVisualDescendants().OfType<AppAttachmentChip>());
                var thumbnail = chip.FindControl<AppAttachmentThumbnail>("ThumbnailImage")!;
                await thumbnail.Loading.WaitAsync(TimeSpan.FromSeconds(5), Token);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal(unavailable == 0, thumbnail.HasThumbnail);
                Assert.Equal(unavailable != 0, chip.FindControl<AppIcon>("FileIcon")!.IsVisible);
                Assert.Equal("history.png", chip.FindControl<AppText>("PathLabel")!.Text);
                Assert.False(chip.FindControl<AppIconButton>("RemoveButton")!.IsVisible);
                Assert.Same(Assert.Single(mapped.Attachments).Thumbnail, chip.Thumbnail);
                Assert.Equal("保留图片说明", mapped.Content);
                SaveEvidence(window, dark, unavailable);
                window.Content = null;
                Assert.False(thumbnail.HasThumbnail);
                Assert.Null(thumbnail.Source);
            }
            finally { window.Close(); }
        }, Token);

    private static void SaveEvidence(AppWindow window, bool dark, int unavailable)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_HISTORY_THUMBNAIL_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"history-dark-{dark}-unavailable-{unavailable}.png"), PngBitmapEncoderOptions.Default);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
