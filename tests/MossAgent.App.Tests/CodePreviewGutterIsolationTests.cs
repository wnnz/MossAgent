using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class CodePreviewGutterIsolationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DifferentPreviews_OwnTheirTransformAndDoNotScrollAnotherGutter(bool dark) => _session.DispatchAsync(() =>
    {
        var left = new AppCodePreview { Code = new string('x', 180) };
        var right = new AppCodePreview { Code = new string('y', 180) };
        Grid.SetColumn(right, 1);
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Children = { left, right } };
        var window = new AppWindow { Content = layout, Width = 640, Height = 260, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            left.FindControl<AppScrollViewer>("Scroller")!.Offset = new Vector(150, 0);
            right.FindControl<AppScrollViewer>("Scroller")!.Offset = new Vector(60, 0);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.NotSame(left.GutterTransform, right.GutterTransform);
            foreach (var preview in new[] { left, right })
            {
                var gutter = Assert.Single(preview.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains("file-code-gutter"));
                Assert.InRange(gutter.TranslatePoint(default, preview)!.Value.X, 0, .01);
                Assert.Equal(preview.Background, gutter.Background);
                var point = gutter.TranslatePoint(new Point(gutter.Bounds.Width / 2, gutter.Bounds.Height / 2), window)!.Value;
                var hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(point));
                Assert.True(ReferenceEquals(hit, gutter) || hit.GetVisualAncestors().Contains(gutter));
                Assert.IsNotType<SyntaxHighlightedSelectableTextBlock>(hit);
            }
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DiffGutters_KeepHeaderAddedRemovedAndContextSurfacesAligned(bool dark) => _session.DispatchAsync(() =>
    {
        var preview = new AppCodePreview { Code = "@@ -1,2 +1,2 @@\n-old\n+" + new string('x', 180) + "\n context", IsDiff = true };
        var window = new AppWindow { Content = preview, Width = 300, Height = 260, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            preview.FindControl<AppScrollViewer>("Scroller")!.Offset = new Vector(150, 0);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var rows = preview.GetVisualDescendants().OfType<AppBorder>().Where(control => control.Classes.Contains("file-code-row")).ToArray();
            Assert.Equal(4, rows.Length);
            Assert.Equal(4, rows.Select(control => ((MossAgent.App.ViewModels.CodePreviewLine)control.DataContext!).Kind).Distinct().Count());
            foreach (var row in rows)
            {
                var gutter = Assert.Single(row.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains("file-code-gutter"));
                Assert.Equal(row.Background ?? preview.Background, gutter.Background);
                Assert.InRange(gutter.TranslatePoint(default, preview)!.Value.X, 0, .01);
                Assert.Equal(row.Bounds.Height, gutter.Bounds.Height);
            }
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
