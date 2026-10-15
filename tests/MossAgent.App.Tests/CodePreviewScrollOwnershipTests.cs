using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class CodePreviewScrollOwnershipTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SearchNavigation_PreservesHorizontalPositionAndGutterHighlight(bool dark) => _session.DispatchAsync(() =>
    {
        var line = "public string Value => \"" + new string('x', 180) + "\";";
        var preview = new AppCodePreview { Code = string.Join('\n', Enumerable.Repeat(line, 80)), FilePath = "test.cs" };
        var window = new AppWindow { Content = preview, Width = 300, Height = 260, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var scroller = preview.FindControl<AppScrollViewer>("Scroller")!;
            scroller.Offset = new Vector(150, 0);
            preview.SearchQuery = "Value";
            preview.SelectedLine = 60;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(150, scroller.Offset.X);
            Assert.True(scroller.Offset.Y > 0);
            var row = Assert.Single(preview.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains("file-code-row") && control.Classes.Contains("current-match"));
            var gutter = Assert.Single(row.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains("file-code-gutter"));
            Assert.Equal(row.Background, gutter.Background);
            Assert.InRange(gutter.TranslatePoint(default, preview)!.Value.X, 0, .01);
            Assert.InRange(gutter.TranslatePoint(default, preview)!.Value.Y, 0, scroller.Viewport.Height);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReplacingContent_ResetsBothOffsetsAndGutterWithoutOldRows(bool diff) => _session.DispatchAsync(() =>
    {
        var line = (diff ? "+" : "") + new string('x', 180);
        var preview = new AppCodePreview { Code = (diff ? "@@ -0,0 +1,80 @@\n" : "") + string.Join('\n', Enumerable.Repeat(line, 80)), IsDiff = diff };
        var window = new AppWindow { Content = preview, Width = 300, Height = 260 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var scroller = preview.FindControl<AppScrollViewer>("Scroller")!;
            scroller.Offset = new Vector(150, 220);
            window.UpdateLayout();
            Assert.Equal(150, scroller.Offset.X);
            preview.Code = diff ? "@@ -0,0 +1 @@\n+replacement" : "replacement";
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(default, scroller.Offset);
            var gutters = preview.GetVisualDescendants().OfType<AppBorder>().Where(control => control.Classes.Contains("file-code-gutter")).ToArray();
            Assert.Equal(diff ? 2 : 1, gutters.Length);
            Assert.All(gutters,
                control => Assert.InRange(control.TranslatePoint(default, preview)!.Value.X, 0, .01));
            Assert.Contains(preview.GetVisualDescendants().OfType<SyntaxHighlightedSelectableTextBlock>(), control => control.Code == "replacement");
            Assert.DoesNotContain(preview.GetVisualDescendants().OfType<SyntaxHighlightedSelectableTextBlock>(), control => control.Code == line.TrimStart('+'));
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
