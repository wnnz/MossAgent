using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ThemeThumbnailShadowLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "light")]
    [InlineData(true, "light")]
    [InlineData(false, "dark")]
    [InlineData(true, "dark")]
    [InlineData(false, "system")]
    [InlineData(true, "system")]
    public Task ShadowAndRoundedContentClippingHaveSeparateOwnersWithMatchingBounds(bool dark, string mode) => _session.Dispatch(() =>
    {
        var preview = new AppThemePreview();
        preview.Classes.Add("preview-" + mode);
        var window = new AppWindow
        {
            Width = 160,
            Height = 120,
            Content = preview,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(new Size(80, 60), preview.Bounds.Size);
            var scenes = preview.GetVisualDescendants().OfType<AppThemePreviewScene>().Where(scene => scene.IsEffectivelyVisible);
            foreach (var scene in scenes)
            {
                var borders = scene.GetVisualDescendants().OfType<AppBorder>().ToArray();
                var shadow = Assert.Single(borders, border => border.Classes.Contains("preview-screen-shadow"));
                var content = Assert.Single(borders, border => border.Classes.Contains("preview-screen"));
                Assert.Equal(new Point(7, 7), shadow.TranslatePoint(default, scene));
                Assert.Equal(new Size(66, 46), shadow.Bounds.Size);
                Assert.Equal(content.Bounds, shadow.Bounds);
                Assert.Equal(content.CornerRadius, shadow.CornerRadius);
                Assert.Same(content.GetVisualParent(), shadow.GetVisualParent());
                Assert.True(content.ClipToBounds);
                Assert.False(shadow.ClipToBounds);
                Assert.False(shadow.IsHitTestVisible);
                Assert.Equal(0, content.BoxShadow.Count);
                Assert.Equal(1, shadow.BoxShadow.Count);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
