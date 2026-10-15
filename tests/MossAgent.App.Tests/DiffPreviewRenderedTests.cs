using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class DiffPreviewRenderedTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 768)]
    [InlineData(true, 768)]
    [InlineData(false, 650)]
    [InlineData(true, 650)]
    public Task DiffPreviewUsesMeasuredRowsAndIndependentHorizontalScroll(bool dark, double width) =>
        _session.Dispatch(() =>
        {
            Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var preview = new AppDiffPreview();
            var window = new AppWindow { Width = width, Height = 200, Content = preview };
            window.Show();
            window.UpdateLayout();
            try
            {
                var scrolls = preview.GetVisualDescendants().OfType<AppScrollViewer>().ToArray();
                Assert.Equal(2, scrolls.Length);
                Assert.All(scrolls, scroll =>
                {
                    Assert.Equal(ScrollBarVisibility.Visible, scroll.HorizontalScrollBarVisibility);
                    Assert.Equal(ScrollBarVisibility.Disabled, scroll.VerticalScrollBarVisibility);
                    Assert.InRange(scroll.Extent.Height - scroll.Viewport.Height, -100, 0.01);
                });
                var rows = preview.GetVisualDescendants().OfType<Grid>()
                    .Where(control => control.Classes.Contains("diff-preview-row")).ToArray();
                Assert.Equal(10, rows.Length);
                Assert.All(rows, row => Assert.InRange(row.Bounds.Height, 21.33, 21.34));
                if (!dark)
                {
                    var removed = preview.GetVisualDescendants().OfType<AppBorder>().First(control => control.Classes.Contains("diff-preview-removed"));
                    var added = preview.GetVisualDescendants().OfType<AppBorder>().First(control => control.Classes.Contains("diff-preview-added"));
                    Assert.Equal(Color.Parse("#FBE6E2"), Assert.IsType<SolidColorBrush>(removed.Background).Color);
                    Assert.Equal(Color.Parse("#E7F4E7"), Assert.IsType<SolidColorBrush>(added.Background).Color);
                    var gutter = removed.GetVisualDescendants().OfType<AppBorder>()
                        .Single(control => control.Classes.Contains("diff-preview-removed-gutter"));
                    Assert.Equal(Color.Parse("#FDECE9"), Assert.IsType<SolidColorBrush>(gutter.Background).Color);
                }
                var identifier = scrolls[0].GetVisualDescendants().OfType<AppText>()
                    .Single(text => text.Classes.Contains("diff-token-identifier"));
                identifier.Text = " " + new string('W', 200);
                window.UpdateLayout();
                Assert.True(scrolls[0].Extent.Width > scrolls[0].Viewport.Width);
                scrolls[0].Offset = new Vector(20, 0);
                window.UpdateLayout();
                Assert.True(scrolls[0].Offset.X > 0);
                Assert.Equal(default, scrolls[1].Offset);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
