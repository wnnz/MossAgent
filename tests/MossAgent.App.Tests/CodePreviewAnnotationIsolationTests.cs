using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class CodePreviewAnnotationIsolationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task PlainCodeDoesNotShowDiffAnnotationsAndResetClearsFocusedMarkers() => _session.Dispatch(() =>
    {
        var preview = new AppCodePreview { Code = "@@ -1 +1 @@\n-old\n+new", IsDiff = true,
            Annotations = [new(11, 1, "RIGHT", "reviewer", "comment")], AnnotationSelection = new(11, 1) };
        var window = new Window { Width = 520, Height = 400, Content = preview };
        window.Show();
        try
        {
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            Assert.Single(preview.GetVisualDescendants().OfType<AppBorder>(), row => row.Classes.Contains("annotation-target"));
            preview.IsDiff = false; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(preview.GetVisualDescendants().OfType<AppCard>(), card => card.IsEffectivelyVisible);
            Assert.DoesNotContain(preview.GetVisualDescendants().OfType<AppBorder>(), row => row.Classes.Contains("annotation-target"));
            preview.IsDiff = true; preview.Annotations = []; preview.AnnotationSelection = null;
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(preview.GetVisualDescendants().OfType<AppCard>(), card => card.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task HorizontalCodeScrollingKeepsCommentCardsWithinTheirOwnViewport() => _session.Dispatch(() =>
    {
        var preview = new AppCodePreview { Code = "@@ -1 +1 @@\n-old\n+" + new string('x', 800), IsDiff = true,
            Annotations = [new(11, 1, "RIGHT", "reviewer", new string('y', 100))] };
        var window = new Window { Width = 520, Height = 400, Content = preview };
        window.Show();
        try
        {
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var scroller = preview.FindControl<AppScrollViewer>("Scroller")!;
            scroller.Offset = new Vector(120, 0); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var card = Assert.Single(preview.GetVisualDescendants().OfType<AppCard>(), item => item.IsEffectivelyVisible);
            Assert.InRange(card.Bounds.Width, 1, scroller.Viewport.Width);
            var origin = card.TranslatePoint(default, window)!.Value;
            Assert.InRange(origin.X, -1, 1);
            Assert.InRange(origin.X + card.Bounds.Width, window.ClientSize.Width - 2, window.ClientSize.Width + 1);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("LEFT")]
    [InlineData("RIGHT")]
    public Task RepeatedLocationScrollsToTheExactOffscreenComment(string side) => _session.Dispatch(() =>
    {
        var patch = "@@ -1,61 +1,61 @@\n" + string.Join('\n', Enumerable.Range(1, 60).Select(line => $" context {line}")) + "\n-old\n+new";
        var preview = new AppCodePreview { Code = patch, IsDiff = true,
            Annotations = [new(10, 61, side, "first reviewer", "other comment"), new(11, 61, side, "reviewer", "target comment")] };
        var window = new Window { Width = 520, Height = 340, Content = preview };
        window.Show();
        try
        {
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var scroller = preview.FindControl<AppScrollViewer>("Scroller")!;
            var initialCard = Assert.Single(preview.GetVisualDescendants().OfType<AppCard>(), item => item.DataContext is CodePreviewAnnotation { Id: 11 });
            Assert.True(initialCard.TranslatePoint(default, window)!.Value.Y > window.ClientSize.Height);
            foreach (var sequence in new[] { 1, 2 })
            {
                scroller.Offset = default;
                preview.AnnotationSelection = new(11, sequence);
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.True(scroller.Offset.Y > 0);
                var card = Assert.Single(preview.GetVisualDescendants().OfType<AppCard>(), item => item.DataContext is CodePreviewAnnotation { Id: 11 });
                Assert.Same(card, window.FocusManager!.GetFocusedElement());
                var origin = card.TranslatePoint(default, window)!.Value;
                Assert.InRange(origin.Y, -1, window.ClientSize.Height - card.Bounds.Height + 1);
            }
            if (Environment.GetEnvironmentVariable("MOSSAGENT_UI_EVIDENCE_DIRECTORY") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
                frame.Save(Path.Combine(folder, $"pr-inline-scroll-{side}.png"), PngBitmapEncoderOptions.Default);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
